using HMS.Application.Common;
using HMS.Domain.Appointments;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HMS.Application.Doctors;

public sealed record DoctorQuery(string? Search, long? DepartmentId, long? SpecializationId, bool? IsActive);

public interface IDoctorService
{
    Task<IReadOnlyList<DoctorListItemDto>> ListAsync(DoctorQuery query, CancellationToken ct);
    Task<DoctorDto> GetAsync(long id, CancellationToken ct);
    Task<DoctorDto> CreateAsync(SaveDoctorRequest request, CancellationToken ct);
    Task<DoctorDto> UpdateAsync(long id, SaveDoctorRequest request, CancellationToken ct);
    Task SetStatusAsync(long id, bool isActive, CancellationToken ct);
    Task<IReadOnlyList<ScheduleBlockDto>> GetScheduleAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<ScheduleBlockDto>> SaveScheduleAsync(long id, SaveScheduleRequest request, CancellationToken ct);
    Task<IReadOnlyList<DoctorLeaveDto>> ListLeavesAsync(long id, CancellationToken ct);
    Task<DoctorLeaveDto> AddLeaveAsync(long id, CreateLeaveRequest request, CancellationToken ct);
    Task DeleteLeaveAsync(long id, long leaveId, CancellationToken ct);
    Task<IReadOnlyList<SlotDto>> GetSlotsAsync(long id, DateOnly date, CancellationToken ct);
    Task<IReadOnlyList<AvailableDoctorDto>> GetAvailableTodayAsync(CancellationToken ct);
}

public sealed class DoctorService(
    IAppDbContext db, IClock clock, ISequenceGenerator sequences, IOptions<HospitalOptions> options) : IDoctorService
{
    public async Task<IReadOnlyList<DoctorListItemDto>> ListAsync(DoctorQuery query, CancellationToken ct)
    {
        var q = db.Doctors.AsNoTracking();
        if (query.DepartmentId is { } dep) q = q.Where(d => d.DepartmentId == dep);
        if (query.SpecializationId is { } spec) q = q.Where(d => d.Specializations.Any(s => s.SpecializationId == spec));
        if (query.IsActive is { } active) q = q.Where(d => d.IsActive == active);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(d => d.FirstName.StartsWith(s) || d.LastName.StartsWith(s) || d.DoctorCode == s);
        }
        return await q.OrderBy(d => d.FirstName).ThenBy(d => d.LastName)
            .Select(d => new DoctorListItemDto(
                d.Id, d.DoctorCode, "Dr. " + d.FirstName + " " + d.LastName, d.DepartmentId, d.Department.Name,
                d.Specializations.Where(s => s.IsPrimary).Select(s => s.Specialization.Name).FirstOrDefault(),
                d.ConsultationFee, d.Phone, d.IsActive))
            .ToListAsync(ct);
    }

    public async Task<DoctorDto> GetAsync(long id, CancellationToken ct)
    {
        var d = await db.Doctors.AsNoTracking()
                    .Include(x => x.Department)
                    .Include(x => x.Specializations).ThenInclude(s => s.Specialization)
                    .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Doctor", id);
        return ToDto(d);
    }

    public async Task<DoctorDto> CreateAsync(SaveDoctorRequest request, CancellationToken ct)
    {
        await ValidateReferencesAsync(request, null, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var doctor = new Doctor { DoctorCode = IdFormats.DoctorCode(await sequences.NextAsync(IdFormats.DoctorSequenceKey, ct)) };
        Apply(doctor, request);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(doctor.Id, ct);
    }

    public async Task<DoctorDto> UpdateAsync(long id, SaveDoctorRequest request, CancellationToken ct)
    {
        var doctor = await db.Doctors.Include(d => d.Specializations).FirstOrDefaultAsync(d => d.Id == id, ct)
                     ?? throw new NotFoundException("Doctor", id);
        await ValidateReferencesAsync(request, id, ct);
        Apply(doctor, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetStatusAsync(long id, bool isActive, CancellationToken ct)
    {
        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException("Doctor", id);
        if (!isActive)
        {
            var today = clock.Today;
            var future = await db.Appointments.AsNoTracking()
                .Where(a => a.DoctorId == id && a.AppointmentDate >= today &&
                            (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Confirmed))
                .OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime)
                .Select(a => new FutureAppointmentRef(a.Id, a.AppointmentDate, a.StartTime, a.Patient.FirstName + " " + a.Patient.LastName))
                .ToListAsync(ct);
            if (future.Count > 0)
                throw new BusinessRuleException("DOCTOR_HAS_FUTURE_APPOINTMENTS",
                        $"Reschedule or cancel {future.Count} upcoming appointment(s) before deactivating this doctor.")
                    .With("appointments", future);
        }
        doctor.IsActive = isActive;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduleBlockDto>> GetScheduleAsync(long id, CancellationToken ct)
    {
        await EnsureExistsAsync(id, ct);
        return await db.DoctorSchedules.AsNoTracking().Where(s => s.DoctorId == id && s.IsActive)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .Select(s => new ScheduleBlockDto(s.DayOfWeek, s.StartTime, s.EndTime, s.SlotDurationMinutes))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduleBlockDto>> SaveScheduleAsync(long id, SaveScheduleRequest request, CancellationToken ct)
    {
        await EnsureExistsAsync(id, ct);
        var blocks = request.Blocks.Select(b => new DoctorSchedule
        {
            DoctorId = id, DayOfWeek = (byte)b.DayOfWeek, StartTime = b.StartTime, EndTime = b.EndTime,
            SlotDurationMinutes = (short)b.SlotDurationMinutes,
        }).ToList();

        var overlaps = SlotGenerator.FindOverlaps(blocks);
        if (overlaps.Count > 0)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["blocks"] = overlaps.ToArray() });

        // The weekly schedule is replaced as a whole; existing bookings are unaffected.
        db.DoctorSchedules.RemoveRange(await db.DoctorSchedules.Where(s => s.DoctorId == id).ToListAsync(ct));
        db.DoctorSchedules.AddRange(blocks);
        await db.SaveChangesAsync(ct);
        return await GetScheduleAsync(id, ct);
    }

    public async Task<IReadOnlyList<DoctorLeaveDto>> ListLeavesAsync(long id, CancellationToken ct)
    {
        await EnsureExistsAsync(id, ct);
        return await db.DoctorLeaves.AsNoTracking().Where(l => l.DoctorId == id).OrderByDescending(l => l.FromDate)
            .Select(l => new DoctorLeaveDto(l.Id, l.FromDate, l.ToDate, l.Reason)).ToListAsync(ct);
    }

    public async Task<DoctorLeaveDto> AddLeaveAsync(long id, CreateLeaveRequest request, CancellationToken ct)
    {
        await EnsureExistsAsync(id, ct);
        var leave = new DoctorLeave { DoctorId = id, FromDate = request.FromDate, ToDate = request.ToDate, Reason = request.Reason };
        db.DoctorLeaves.Add(leave);
        await db.SaveChangesAsync(ct);
        return new DoctorLeaveDto(leave.Id, leave.FromDate, leave.ToDate, leave.Reason);
    }

    public async Task DeleteLeaveAsync(long id, long leaveId, CancellationToken ct)
    {
        var leave = await db.DoctorLeaves.FirstOrDefaultAsync(l => l.Id == leaveId && l.DoctorId == id, ct)
                    ?? throw new NotFoundException("Leave", leaveId);
        db.DoctorLeaves.Remove(leave);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SlotDto>> GetSlotsAsync(long id, DateOnly date, CancellationToken ct)
    {
        var doctor = await db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct)
                     ?? throw new NotFoundException("Doctor", id);
        if (!doctor.IsActive) return [];

        var all = await ComputeSlotsAsync(id, date, ct);
        var today = clock.Today;
        var now = clock.LocalTime;
        var maxDate = today.AddDays(options.Value.MaxDaysAhead);
        return all.Select(s => s with
        {
            IsAvailable = s.IsAvailable && date >= today && date <= maxDate && (date > today || s.StartTime > now),
        }).ToList();
    }

    public async Task<IReadOnlyList<AvailableDoctorDto>> GetAvailableTodayAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var weekday = (byte)today.DayOfWeek;
        var doctors = await db.Doctors.AsNoTracking()
            .Where(d => d.IsActive
                        && d.Schedules.Any(s => s.IsActive && s.DayOfWeek == weekday)
                        && !d.Leaves.Any(l => l.FromDate <= today && l.ToDate >= today))
            .Select(d => new
            {
                d.Id, d.FirstName, d.LastName, Department = d.Department.Name,
                Primary = d.Specializations.Where(s => s.IsPrimary).Select(s => s.Specialization.Name).FirstOrDefault(),
                Blocks = d.Schedules.Where(s => s.IsActive && s.DayOfWeek == weekday).OrderBy(s => s.StartTime).ToList(),
            })
            .ToListAsync(ct);

        var ids = doctors.Select(d => d.Id).ToList();
        var todays = await db.Appointments.AsNoTracking()
            .Where(a => ids.Contains(a.DoctorId) && a.AppointmentDate == today)
            .Select(a => new { a.DoctorId, a.Status, a.StartTime })
            .ToListAsync(ct);

        var now = clock.LocalTime;
        return doctors.Select(d =>
        {
            var held = todays.Where(a => a.DoctorId == d.Id && !Appointment.SlotReleasingStatuses.Contains(a.Status))
                .Select(a => a.StartTime).ToHashSet();
            var free = SlotGenerator.Generate(today, d.Blocks, []).Count(s => s.Start > now && !held.Contains(s.Start));
            var waiting = todays.Count(a => a.DoctorId == d.Id && a.Status == AppointmentStatus.CheckedIn);
            var hours = string.Join(", ", d.Blocks.Select(b => $"{b.StartTime:HH\\:mm}-{b.EndTime:HH\\:mm}"));
            return new AvailableDoctorDto(d.Id, $"Dr. {d.FirstName} {d.LastName}", d.Department, d.Primary, hours, free, waiting);
        }).ToList();
    }

    /// <summary>All schedule slots for the date, marked unavailable when held by an active appointment.</summary>
    internal async Task<List<SlotDto>> ComputeSlotsAsync(long doctorId, DateOnly date, CancellationToken ct)
    {
        var schedules = await db.DoctorSchedules.AsNoTracking().Where(s => s.DoctorId == doctorId).ToListAsync(ct);
        var leaves = await db.DoctorLeaves.AsNoTracking()
            .Where(l => l.DoctorId == doctorId && l.FromDate <= date && l.ToDate >= date).ToListAsync(ct);
        var held = await db.Appointments.AsNoTracking()
            .Where(a => a.DoctorId == doctorId && a.AppointmentDate == date && !Appointment.SlotReleasingStatuses.Contains(a.Status))
            .Select(a => a.StartTime).ToListAsync(ct);

        return SlotGenerator.Generate(date, schedules, leaves)
            .Select(s => new SlotDto(s.Start, s.End, !held.Contains(s.Start)))
            .ToList();
    }

    private async Task EnsureExistsAsync(long id, CancellationToken ct)
    {
        if (!await db.Doctors.AnyAsync(d => d.Id == id, ct)) throw new NotFoundException("Doctor", id);
    }

    private async Task ValidateReferencesAsync(SaveDoctorRequest request, long? id, CancellationToken ct)
    {
        if (!await db.Departments.AnyAsync(d => d.Id == request.DepartmentId, ct))
            throw new NotFoundException("Department", request.DepartmentId);
        var found = await db.Specializations.CountAsync(s => request.SpecializationIds.Contains(s.Id), ct);
        if (found != request.SpecializationIds.Distinct().Count())
            throw new BusinessRuleException("UNKNOWN_SPECIALIZATION", "One or more specializations do not exist.");
        if (await db.Doctors.IgnoreQueryFilters().AnyAsync(d => d.RegistrationNumber == request.RegistrationNumber && d.Id != id, ct))
            throw new ConflictException("DUPLICATE_REGISTRATION_NUMBER", "Another doctor has this registration number.");
    }

    private static void Apply(Doctor doctor, SaveDoctorRequest r)
    {
        doctor.FirstName = r.FirstName.Trim();
        doctor.LastName = r.LastName.Trim();
        doctor.Gender = r.Gender;
        doctor.Phone = Patient.NormalisePhone(r.Phone);
        doctor.Email = r.Email;
        doctor.Qualification = r.Qualification.Trim();
        doctor.RegistrationNumber = r.RegistrationNumber.Trim();
        doctor.DepartmentId = r.DepartmentId;
        doctor.ConsultationFee = r.ConsultationFee;
        doctor.YearsOfExperience = r.YearsOfExperience;

        doctor.Specializations.RemoveAll(s => !r.SpecializationIds.Contains(s.SpecializationId));
        foreach (var specId in r.SpecializationIds.Distinct())
        {
            var link = doctor.Specializations.FirstOrDefault(s => s.SpecializationId == specId);
            if (link is null)
            {
                link = new DoctorSpecialization { SpecializationId = specId };
                doctor.Specializations.Add(link);
            }
            link.IsPrimary = specId == r.PrimarySpecializationId;
        }
    }

    private static DoctorDto ToDto(Doctor d) => new(
        d.Id, d.DoctorCode, d.FirstName, d.LastName, d.FullName, d.Gender, d.Phone, d.Email, d.Qualification,
        d.RegistrationNumber, d.DepartmentId, d.Department.Name, d.ConsultationFee, d.YearsOfExperience, d.IsActive,
        d.Specializations.OrderByDescending(s => s.IsPrimary).ThenBy(s => s.Specialization.Name)
            .Select(s => new DoctorSpecializationDto(s.SpecializationId, s.Specialization.Name, s.IsPrimary)).ToList());
}
