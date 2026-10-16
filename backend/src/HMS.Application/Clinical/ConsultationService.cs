using HMS.Application.Common;
using HMS.Domain.Appointments;
using HMS.Domain.Clinical;
using HMS.Domain.Common;
using HMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Clinical;

public interface IConsultationService
{
    Task<ConsultationDto> StartAsync(StartConsultationRequest request, CancellationToken ct);
    Task<ConsultationDto> GetAsync(long id, CancellationToken ct);
    Task<ConsultationDto> UpdateAsync(long id, UpdateConsultationRequest request, CancellationToken ct);
    Task<ConsultationDto> FinaliseAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<RevisionDto>> GetHistoryAsync(long id, CancellationToken ct);
    Task<VitalSignsDto> AddVitalsAsync(long id, SaveVitalsRequest request, CancellationToken ct);
    Task<VitalSignsDto> UpdateVitalsAsync(long id, long vitalsId, SaveVitalsRequest request, CancellationToken ct);
    Task<IReadOnlyList<VitalSignsDto>> GetPatientVitalsAsync(long patientId, DateOnly? from, DateOnly? to, CancellationToken ct);
}

public sealed class ConsultationService(
    IAppDbContext db, IClock clock, ICurrentUser currentUser, ClinicalRevisionWriter revisions) : IConsultationService
{
    public async Task<ConsultationDto> StartAsync(StartConsultationRequest request, CancellationToken ct)
    {
        var doctorId = currentUser.DoctorId
                       ?? throw new ForbiddenException("Only doctors can start a consultation.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Consultation consultation;
        if (request.AppointmentId is { } appointmentId)
        {
            var appointment = await db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId, ct)
                              ?? throw new NotFoundException("Appointment", appointmentId);
            if (appointment.DoctorId != doctorId)
                throw new ForbiddenException("This appointment belongs to another doctor.");

            var existing = await db.Consultations.Where(c => c.AppointmentId == appointmentId).Select(c => (long?)c.Id).FirstOrDefaultAsync(ct);
            if (existing is { } existingId)
                return await GetAsync(existingId, ct);

            appointment.TransitionTo(AppointmentStatus.InConsultation, clock.UtcNow, currentUser.UserId);
            consultation = new Consultation { PatientId = appointment.PatientId, DoctorId = doctorId, AppointmentId = appointmentId };
        }
        else
        {
            var patientId = request.PatientId!.Value;
            if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct)) throw new NotFoundException("Patient", patientId);
            consultation = new Consultation { PatientId = patientId, DoctorId = doctorId };
        }

        consultation.StartedAt = clock.UtcNow;
        db.Consultations.Add(consultation);
        await db.SaveChangesAsync(ct);
        await revisions.AddAsync(ClinicalEntityType.Consultation, consultation.Id, consultation.Id, RevisionChangeType.Created, null, Snapshot(consultation), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(consultation.Id, ct);
    }

    public async Task<ConsultationDto> GetAsync(long id, CancellationToken ct)
    {
        var c = await db.Consultations.AsNoTracking()
                    .Include(x => x.Patient).ThenInclude(p => p.Allergies)
                    .Include(x => x.Patient).ThenInclude(p => p.Conditions)
                    .Include(x => x.Doctor)
                    .Include(x => x.Symptoms)
                    .Include(x => x.Vitals)
                    .Include(x => x.Prescription)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Consultation", id);

        var names = await UserNamesAsync(c.Vitals.Select(v => v.RecordedBy), ct);
        var p = c.Patient;
        return new ConsultationDto(
            c.Id,
            new ConsultationPatientDto(p.Id, p.Mrn, p.FullName, p.Gender, p.AgeOn(clock.Today), p.BloodGroup,
                p.Allergies.Select(a => a.Allergen).ToList(), p.Conditions.Select(x => x.ConditionName).ToList()),
            c.DoctorId, c.Doctor.FullName, c.AppointmentId, c.Status, c.StartedAt, c.FinalisedAt,
            c.ChiefComplaint, c.HistoryOfPresentIllness, c.ExaminationFindings, c.Diagnosis, c.DiagnosisIcd10,
            c.ClinicalNotes, c.Advice,
            c.Symptoms.OrderBy(s => s.SortOrder).Select(s => new SymptomDto(s.Symptom, s.DurationValue, s.DurationUnit, s.Severity)).ToList(),
            c.Vitals.OrderByDescending(v => v.RecordedAt).Select(v => ToDto(v, names)).ToList(),
            c.Prescription?.Id, c.RowVersion, CanEdit(c.DoctorId));
    }

    public async Task<ConsultationDto> UpdateAsync(long id, UpdateConsultationRequest request, CancellationToken ct)
    {
        var c = await db.Consultations.Include(x => x.Symptoms).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Consultation", id);
        currentUser.EnsureOwnsClinicalRecord(c.DoctorId);
        c.EnsureRowVersion(request.RowVersion);
        var changeType = ResolveChangeType(c.Status, request.AmendmentReason);

        c.ChiefComplaint = Clean(request.ChiefComplaint);
        c.HistoryOfPresentIllness = Clean(request.HistoryOfPresentIllness);
        c.ExaminationFindings = Clean(request.ExaminationFindings);
        c.Diagnosis = Clean(request.Diagnosis);
        c.DiagnosisIcd10 = Clean(request.DiagnosisIcd10)?.ToUpperInvariant();
        c.ClinicalNotes = Clean(request.ClinicalNotes);
        c.Advice = Clean(request.Advice);

        db.ConsultationSymptoms.RemoveRange(c.Symptoms);
        c.Symptoms = request.Symptoms.Select((s, i) => new ConsultationSymptom
        {
            Symptom = s.Symptom.Trim(), DurationValue = s.DurationValue, DurationUnit = s.DurationUnit,
            Severity = s.Severity, SortOrder = (short)i,
        }).ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await revisions.AddAsync(ClinicalEntityType.Consultation, c.Id, c.Id, changeType, request.AmendmentReason?.Trim(), Snapshot(c), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<ConsultationDto> FinaliseAsync(long id, CancellationToken ct)
    {
        var c = await db.Consultations.Include(x => x.Symptoms).Include(x => x.Appointment)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Consultation", id);
        currentUser.EnsureOwnsClinicalRecord(c.DoctorId);
        c.Finalise(clock.UtcNow);
        if (c.Appointment is { Status: AppointmentStatus.InConsultation } appointment)
            appointment.TransitionTo(AppointmentStatus.Completed, clock.UtcNow, currentUser.UserId);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await revisions.AddAsync(ClinicalEntityType.Consultation, c.Id, c.Id, RevisionChangeType.Finalised, null, Snapshot(c), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<IReadOnlyList<RevisionDto>> GetHistoryAsync(long id, CancellationToken ct)
    {
        if (!await db.Consultations.AnyAsync(c => c.Id == id, ct)) throw new NotFoundException("Consultation", id);
        return await revisions.ListAsync(id, null, null, ct);
    }

    public async Task<VitalSignsDto> AddVitalsAsync(long id, SaveVitalsRequest request, CancellationToken ct)
    {
        var c = await db.Consultations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Consultation", id);
        EnsureCanRecordVitals(c);
        var changeType = ResolveChangeType(c.Status, request.AmendmentReason) == RevisionChangeType.Amended
            ? RevisionChangeType.Amended : RevisionChangeType.Created;

        var vitals = new VitalSigns { ConsultationId = id, PatientId = c.PatientId, RecordedAt = clock.UtcNow, RecordedBy = currentUser.UserId };
        Apply(vitals, request);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.VitalSigns.Add(vitals);
        await db.SaveChangesAsync(ct);
        await revisions.AddAsync(ClinicalEntityType.VitalSigns, vitals.Id, id, changeType, request.AmendmentReason?.Trim(), Snapshot(vitals), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(vitals, await UserNamesAsync([vitals.RecordedBy], ct));
    }

    public async Task<VitalSignsDto> UpdateVitalsAsync(long id, long vitalsId, SaveVitalsRequest request, CancellationToken ct)
    {
        var c = await db.Consultations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Consultation", id);
        EnsureCanRecordVitals(c);
        var vitals = await db.VitalSigns.FirstOrDefaultAsync(v => v.Id == vitalsId && v.ConsultationId == id, ct)
                     ?? throw new NotFoundException("Vital signs", vitalsId);
        var changeType = ResolveChangeType(c.Status, request.AmendmentReason);
        Apply(vitals, request);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await revisions.AddAsync(ClinicalEntityType.VitalSigns, vitals.Id, id, changeType, request.AmendmentReason?.Trim(), Snapshot(vitals), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(vitals, await UserNamesAsync([vitals.RecordedBy], ct));
    }

    public async Task<IReadOnlyList<VitalSignsDto>> GetPatientVitalsAsync(long patientId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var q = db.VitalSigns.AsNoTracking().Where(v => v.PatientId == patientId);
        if (from is { } f)
        {
            var start = clock.UtcRangeFor(f).StartUtc;
            q = q.Where(v => v.RecordedAt >= start);
        }
        if (to is { } t)
        {
            var end = clock.UtcRangeFor(t).EndUtc;
            q = q.Where(v => v.RecordedAt < end);
        }
        var rows = await q.OrderBy(v => v.RecordedAt).Take(500).ToListAsync(ct);
        var names = await UserNamesAsync(rows.Select(v => v.RecordedBy), ct);
        return rows.Select(v => ToDto(v, names)).ToList();
    }

    private bool CanEdit(long doctorId) => currentUser.IsAdmin() || (currentUser.Role == Role.Doctor && currentUser.DoctorId == doctorId);

    private void EnsureCanRecordVitals(Consultation c)
    {
        if (currentUser.Role == Role.Nurse) return;
        currentUser.EnsureOwnsClinicalRecord(c.DoctorId);
    }

    /// <summary>Draft edits are plain updates; edits after finalising are amendments and need a reason.</summary>
    private static RevisionChangeType ResolveChangeType(ClinicalStatus status, string? amendmentReason)
    {
        if (status == ClinicalStatus.Draft) return RevisionChangeType.Updated;
        if (string.IsNullOrWhiteSpace(amendmentReason))
            throw new BusinessRuleException("AMENDMENT_REASON_REQUIRED", "This record is finalised. Enter a reason for the amendment.");
        return RevisionChangeType.Amended;
    }

    private async Task<Dictionary<long, string>> UserNamesAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    private static void Apply(VitalSigns v, SaveVitalsRequest r)
    {
        v.TemperatureC = r.TemperatureC;
        v.PulseBpm = r.PulseBpm;
        v.BpSystolic = r.BpSystolic;
        v.BpDiastolic = r.BpDiastolic;
        v.RespiratoryRate = r.RespiratoryRate;
        v.Spo2Percent = r.Spo2Percent;
        v.WeightKg = r.WeightKg;
        v.HeightCm = r.HeightCm;
        v.Notes = Clean(r.Notes);
        v.ComputeBmi();
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static VitalSignsDto ToDto(VitalSigns v, IReadOnlyDictionary<long, string> names) => new(
        v.Id, v.RecordedAt, names.GetValueOrDefault(v.RecordedBy), v.TemperatureC, v.PulseBpm, v.BpSystolic, v.BpDiastolic,
        v.RespiratoryRate, v.Spo2Percent, v.WeightKg, v.HeightCm, v.Bmi, v.Notes, v.AbnormalWarnings());

    private static ConsultationSnapshot Snapshot(Consultation c) => new(
        c.Status, c.ChiefComplaint, c.HistoryOfPresentIllness, c.ExaminationFindings, c.Diagnosis, c.DiagnosisIcd10,
        c.ClinicalNotes, c.Advice,
        c.Symptoms.OrderBy(s => s.SortOrder).Select(s => new SymptomDto(s.Symptom, s.DurationValue, s.DurationUnit, s.Severity)).ToList());

    private static VitalsSnapshot Snapshot(VitalSigns v) => new(
        v.TemperatureC, v.PulseBpm, v.BpSystolic, v.BpDiastolic, v.RespiratoryRate, v.Spo2Percent, v.WeightKg, v.HeightCm, v.Bmi, v.Notes);
}
