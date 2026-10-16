using HMS.Application.Billing;
using HMS.Application.Common;
using HMS.Domain.Appointments;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HMS.Application.Appointments;

public interface IAppointmentService
{
    Task<PagedResult<AppointmentDto>> ListAsync(AppointmentQuery query, PageQuery page, CancellationToken ct);
    Task<AppointmentDto> GetAsync(long id, CancellationToken ct);
    Task<AppointmentDto> CreateAsync(CreateAppointmentRequest request, CancellationToken ct);
    Task<AppointmentDto> RescheduleAsync(long id, RescheduleAppointmentRequest request, CancellationToken ct);
    Task<AppointmentDto> ConfirmAsync(long id, CancellationToken ct);
    Task<AppointmentDto> CheckInAsync(long id, CancellationToken ct);
    Task<AppointmentDto> CancelAsync(long id, CancelAppointmentRequest request, CancellationToken ct);
    Task<AppointmentDto> MarkNoShowAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<AppointmentDto>> GetQueueAsync(DateOnly date, long? doctorId, CancellationToken ct);
}

public sealed class AppointmentService(
    IAppDbContext db, IClock clock, ICurrentUser currentUser, IBillingService billing, IOptions<HospitalOptions> options)
    : IAppointmentService
{
    public const string ActiveSlotIndex = "UX_appointments_active_slot";
    public const string QueueTokenIndex = "UX_appointments_queue_token";

    public async Task<PagedResult<AppointmentDto>> ListAsync(AppointmentQuery query, PageQuery page, CancellationToken ct)
    {
        var q = db.Appointments.AsNoTracking();
        if (query.Date is { } date) q = q.Where(a => a.AppointmentDate == date);
        if (query.FromDate is { } from) q = q.Where(a => a.AppointmentDate >= from);
        if (query.ToDate is { } to) q = q.Where(a => a.AppointmentDate <= to);
        if (query.DoctorId is { } doctorId) q = q.Where(a => a.DoctorId == doctorId);
        if (query.PatientId is { } patientId) q = q.Where(a => a.PatientId == patientId);
        if (query.Status is { } status) q = q.Where(a => a.Status == status);

        return await Project(q.OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime)).ToPagedAsync(page, ct);
    }

    public async Task<AppointmentDto> GetAsync(long id, CancellationToken ct)
    {
        var dto = await Project(db.Appointments.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException("Appointment", id);
        var history = await db.AppointmentStatusHistory.AsNoTracking().Where(h => h.AppointmentId == id)
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new AppointmentStatusHistoryDto(h.FromStatus, h.ToStatus, h.OldDate, h.OldStartTime, h.Note, h.ChangedAt,
                db.Users.Where(u => u.Id == h.ChangedBy).Select(u => u.FullName).FirstOrDefault()))
            .ToListAsync(ct);
        return dto with { History = history };
    }

    public async Task<AppointmentDto> CreateAsync(CreateAppointmentRequest request, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == request.PatientId, ct))
            throw new NotFoundException("Patient", request.PatientId);
        await EnsureDoctorBookableAsync(request.DoctorId, ct);

        var isWalkIn = request.AppointmentType == AppointmentType.WalkIn;
        if (isWalkIn && request.AppointmentDate != clock.Today)
            throw new BusinessRuleException("WALK_IN_TODAY_ONLY", "Walk-in appointments can only be booked for today.");

        var slot = isWalkIn && request.StartTime is null
            ? await NextFreeSlotTodayAsync(request.DoctorId, ct)
            : await ValidateSlotAsync(request.DoctorId, request.AppointmentDate, request.StartTime!.Value, null, ct);

        await EnsurePatientNotDoubleBookedAsync(request.PatientId, request.DoctorId, request.AppointmentDate, null, ct);

        var appointment = new Appointment
        {
            PatientId = request.PatientId, DoctorId = request.DoctorId, AppointmentDate = request.AppointmentDate,
            StartTime = slot.Start, EndTime = slot.End, AppointmentType = request.AppointmentType,
            ReasonForVisit = request.ReasonForVisit, FollowUpOfConsultationId = request.FollowUpOfConsultationId,
        };
        appointment.History.Add(new AppointmentStatusHistory
        {
            ToStatus = AppointmentStatus.Booked, ChangedAt = clock.UtcNow, ChangedBy = currentUser.UserId,
            Note = isWalkIn ? "Walk-in" : null,
        });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Appointments.Add(appointment);
        await SaveSlotChangesAsync(ct);
        if (isWalkIn)
            await CheckInCoreAsync(appointment, ct);
        await tx.CommitAsync(ct);
        return await GetAsync(appointment.Id, ct);
    }

    public async Task<AppointmentDto> RescheduleAsync(long id, RescheduleAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        appointment.EnsureRowVersion(request.RowVersion);
        await EnsureDoctorBookableAsync(appointment.DoctorId, ct);

        var slot = await ValidateSlotAsync(appointment.DoctorId, request.AppointmentDate, request.StartTime, appointment.Id, ct);
        if (request.AppointmentDate != appointment.AppointmentDate)
            await EnsurePatientNotDoubleBookedAsync(appointment.PatientId, appointment.DoctorId, request.AppointmentDate, appointment.Id, ct);

        appointment.Reschedule(request.AppointmentDate, slot.Start, slot.End, clock.UtcNow, currentUser.UserId);
        await SaveSlotChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AppointmentDto> ConfirmAsync(long id, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        appointment.TransitionTo(AppointmentStatus.Confirmed, clock.UtcNow, currentUser.UserId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AppointmentDto> CheckInAsync(long id, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        if (appointment.AppointmentDate != clock.Today)
            throw new BusinessRuleException("CHECK_IN_TODAY_ONLY", "Patients can only be checked in on the day of the appointment.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await CheckInCoreAsync(appointment, ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AppointmentDto> CancelAsync(long id, CancelAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        appointment.Cancel(request.Reason.Trim(), clock.UtcNow, currentUser.UserId);

        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == id, ct);
        invoice?.Cancel($"Appointment cancelled: {request.Reason.Trim()}");

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AppointmentDto> MarkNoShowAsync(long id, CancellationToken ct)
    {
        var appointment = await LoadAsync(id, ct);
        var today = clock.Today;
        if (appointment.AppointmentDate > today || (appointment.AppointmentDate == today && appointment.EndTime > clock.LocalTime))
            throw new BusinessRuleException("NO_SHOW_TOO_EARLY", "An appointment can only be marked no-show after its slot has passed.");
        appointment.TransitionTo(AppointmentStatus.NoShow, clock.UtcNow, currentUser.UserId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<AppointmentDto>> GetQueueAsync(DateOnly date, long? doctorId, CancellationToken ct)
    {
        var q = db.Appointments.AsNoTracking().Where(a => a.AppointmentDate == date && a.Status != AppointmentStatus.Cancelled);
        if (doctorId is { } d) q = q.Where(a => a.DoctorId == d);
        // Checked-in patients by token first, then everyone else by slot time.
        return await Project(q.OrderBy(a => a.QueueToken == null).ThenBy(a => a.QueueToken).ThenBy(a => a.StartTime))
            .ToListAsync(ct);
    }

    private async Task CheckInCoreAsync(Appointment appointment, CancellationToken ct)
    {
        appointment.TransitionTo(AppointmentStatus.CheckedIn, clock.UtcNow, currentUser.UserId);
        var lastToken = await db.Appointments
            .Where(a => a.DoctorId == appointment.DoctorId && a.AppointmentDate == appointment.AppointmentDate)
            .MaxAsync(a => a.QueueToken, ct);
        appointment.QueueToken = (short)((lastToken ?? 0) + 1);
        appointment.CheckedInAt = clock.UtcNow;
        await billing.CreateConsultationInvoiceAsync(appointment, ct);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, QueueTokenIndex))
        {
            throw new ConflictException("CONCURRENCY_CONFLICT", "Another patient was checked in at the same moment. Please try again.");
        }
    }

    private async Task SaveSlotChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, ActiveSlotIndex))
        {
            throw new ConflictException("SLOT_UNAVAILABLE", "The selected slot is no longer available.");
        }
    }

    private async Task<Appointment> LoadAsync(long id, CancellationToken ct) =>
        await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Appointment", id);

    private async Task EnsureDoctorBookableAsync(long doctorId, CancellationToken ct)
    {
        var doctor = await db.Doctors.AsNoTracking().Select(d => new { d.Id, d.IsActive }).FirstOrDefaultAsync(d => d.Id == doctorId, ct)
                     ?? throw new NotFoundException("Doctor", doctorId);
        if (!doctor.IsActive)
            throw new BusinessRuleException("DOCTOR_INACTIVE", "This doctor is not accepting appointments.");
    }

    private async Task EnsurePatientNotDoubleBookedAsync(long patientId, long doctorId, DateOnly date, long? exceptId, CancellationToken ct)
    {
        var exists = await db.Appointments.AnyAsync(a => a.PatientId == patientId && a.DoctorId == doctorId &&
                                                         a.AppointmentDate == date && a.Id != exceptId &&
                                                         !Appointment.SlotReleasingStatuses.Contains(a.Status), ct);
        if (exists)
            throw new BusinessRuleException("PATIENT_ALREADY_BOOKED", "This patient already has an appointment with this doctor on that day.");
    }

    /// <summary>Checks the slot is in the doctor's schedule, in the booking window, not in the past and not taken.</summary>
    private async Task<TimeSlot> ValidateSlotAsync(long doctorId, DateOnly date, TimeOnly start, long? exceptAppointmentId, CancellationToken ct)
    {
        var today = clock.Today;
        if (date < today || date > today.AddDays(options.Value.MaxDaysAhead))
            throw new BusinessRuleException("DATE_OUT_OF_RANGE",
                $"Appointments can be booked from today up to {options.Value.MaxDaysAhead} days ahead.");

        var slots = await GenerateSlotsAsync(doctorId, date, ct);
        var slot = slots.FirstOrDefault(s => s.Start == start);
        if (slot == default)
            throw new BusinessRuleException("SLOT_NOT_IN_SCHEDULE", "The doctor is not available at the selected time.");
        if (date == today && slot.Start <= clock.LocalTime)
            throw new BusinessRuleException("SLOT_IN_PAST", "The selected slot has already started.");

        var taken = await db.Appointments.AnyAsync(a => a.DoctorId == doctorId && a.AppointmentDate == date &&
                                                        a.StartTime == start && a.Id != exceptAppointmentId &&
                                                        !Appointment.SlotReleasingStatuses.Contains(a.Status), ct);
        if (taken)
            throw new ConflictException("SLOT_UNAVAILABLE", "The selected slot is no longer available.");
        return slot;
    }

    /// <summary>Walk-ins take the first free slot that hasn't ended yet (the current one counts).</summary>
    private async Task<TimeSlot> NextFreeSlotTodayAsync(long doctorId, CancellationToken ct)
    {
        var today = clock.Today;
        var now = clock.LocalTime;
        var held = await db.Appointments.Where(a => a.DoctorId == doctorId && a.AppointmentDate == today &&
                                                    !Appointment.SlotReleasingStatuses.Contains(a.Status))
            .Select(a => a.StartTime).ToListAsync(ct);
        var slots = await GenerateSlotsAsync(doctorId, today, ct);
        var free = slots.FirstOrDefault(s => s.End > now && !held.Contains(s.Start));
        if (free == default)
            throw new BusinessRuleException("NO_SLOTS_AVAILABLE", "The doctor has no free slots left today.");
        return free;
    }

    private async Task<IReadOnlyList<TimeSlot>> GenerateSlotsAsync(long doctorId, DateOnly date, CancellationToken ct)
    {
        var schedules = await db.DoctorSchedules.AsNoTracking().Where(s => s.DoctorId == doctorId).ToListAsync(ct);
        var leaves = await db.DoctorLeaves.AsNoTracking()
            .Where(l => l.DoctorId == doctorId && l.FromDate <= date && l.ToDate >= date).ToListAsync(ct);
        return SlotGenerator.Generate(date, schedules, leaves);
    }

    private IQueryable<AppointmentDto> Project(IQueryable<Appointment> q) =>
        q.Select(a => new AppointmentDto(
            a.Id, a.PatientId, a.Patient.Mrn, a.Patient.FirstName + " " + a.Patient.LastName, a.Patient.Phone,
            a.DoctorId, "Dr. " + a.Doctor.FirstName + " " + a.Doctor.LastName, a.Doctor.Department.Name,
            a.AppointmentDate, a.StartTime, a.EndTime, a.Status, a.AppointmentType, a.ReasonForVisit,
            a.QueueToken, a.CheckedInAt, a.CancellationReason, a.RescheduleCount,
            db.Consultations.Where(c => c.AppointmentId == a.Id).Select(c => (long?)c.Id).FirstOrDefault(),
            db.Invoices.Where(i => i.AppointmentId == a.Id).Select(i => (long?)i.Id).FirstOrDefault(),
            a.RowVersion, null));
}
