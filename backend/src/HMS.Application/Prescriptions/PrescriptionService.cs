using HMS.Application.Clinical;
using HMS.Application.Common;
using HMS.Domain.Clinical;
using HMS.Domain.Common;
using HMS.Domain.Identity;
using HMS.Domain.Prescriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HMS.Application.Prescriptions;

public interface IPrescriptionService
{
    Task<PrescriptionDto> CreateAsync(long consultationId, SavePrescriptionRequest request, CancellationToken ct);
    Task<PrescriptionDto> GetAsync(long id, CancellationToken ct);
    Task<PrescriptionDto> UpdateAsync(long id, SavePrescriptionRequest request, CancellationToken ct);
    Task<PrescriptionDto> FinaliseAsync(long id, CancellationToken ct);
    Task<PrescriptionPrintDto> GetPrintAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<RevisionDto>> GetHistoryAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<PrescriptionListItemDto>> ListForPatientAsync(long patientId, CancellationToken ct);
}

public sealed class PrescriptionService(
    IAppDbContext db, IClock clock, ICurrentUser currentUser, ISequenceGenerator sequences,
    ClinicalRevisionWriter revisions, IOptions<HospitalOptions> hospital) : IPrescriptionService
{
    public async Task<PrescriptionDto> CreateAsync(long consultationId, SavePrescriptionRequest request, CancellationToken ct)
    {
        var consultation = await db.Consultations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == consultationId, ct)
                           ?? throw new NotFoundException("Consultation", consultationId);
        currentUser.EnsureOwnsClinicalRecord(consultation.DoctorId);
        if (await db.Prescriptions.AnyAsync(p => p.ConsultationId == consultationId, ct))
            throw new ConflictException("PRESCRIPTION_EXISTS", "This consultation already has a prescription. Edit it instead.");

        var items = await BuildItemsAsync(request.Items, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var today = clock.Today;
        var prescription = new Prescription
        {
            PrescriptionNumber = IdFormats.PrescriptionNumber(today, await sequences.NextAsync(IdFormats.PrescriptionSequenceKey(today), ct)),
            ConsultationId = consultationId, PatientId = consultation.PatientId, DoctorId = consultation.DoctorId,
            GeneralInstructions = Clean(request.GeneralInstructions), FollowUpDate = request.FollowUpDate, Items = items,
        };
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync(ct);
        await revisions.AddAsync(ClinicalEntityType.Prescription, prescription.Id, consultationId, RevisionChangeType.Created, null, Snapshot(prescription), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(prescription.Id, ct);
    }

    public async Task<PrescriptionDto> GetAsync(long id, CancellationToken ct)
    {
        var p = await db.Prescriptions.AsNoTracking()
                    .Include(x => x.Items).Include(x => x.Doctor)
                    .Include(x => x.Patient).ThenInclude(x => x.Allergies)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Prescription", id);

        var medicineIds = p.Items.Where(i => i.MedicineId.HasValue).Select(i => i.MedicineId!.Value).ToList();
        var generics = await db.Medicines.AsNoTracking().Where(m => medicineIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.GenericName, ct);

        var canEdit = currentUser.IsAdmin() || (currentUser.Role == Role.Doctor && currentUser.DoctorId == p.DoctorId);
        return new PrescriptionDto(p.Id, p.PrescriptionNumber, p.ConsultationId, p.PatientId, p.Patient.FullName, p.Patient.Mrn,
            p.DoctorId, p.Doctor.FullName, p.Status, p.GeneralInstructions, p.FollowUpDate, p.FinalisedAt, p.CreatedAt,
            ToItemDtos(p.Items), p.RowVersion, canEdit,
            AllergyWarnings(p.Patient.Allergies.Select(a => a.Allergen), p.Items, generics));
    }

    public async Task<PrescriptionDto> UpdateAsync(long id, SavePrescriptionRequest request, CancellationToken ct)
    {
        var p = await db.Prescriptions.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Prescription", id);
        currentUser.EnsureOwnsClinicalRecord(p.DoctorId);
        p.EnsureRowVersion(request.RowVersion);

        RevisionChangeType changeType;
        if (p.Status == ClinicalStatus.Draft) changeType = RevisionChangeType.Updated;
        else if (string.IsNullOrWhiteSpace(request.AmendmentReason))
            throw new BusinessRuleException("AMENDMENT_REASON_REQUIRED", "This prescription is finalised. Enter a reason for the amendment.");
        else changeType = RevisionChangeType.Amended;

        var items = await BuildItemsAsync(request.Items, ct);
        if (p.Status == ClinicalStatus.Finalised && items.Count == 0)
            throw new BusinessRuleException("PRESCRIPTION_EMPTY", "A finalised prescription must keep at least one medicine.");

        db.PrescriptionItems.RemoveRange(p.Items);
        p.Items = items;
        p.GeneralInstructions = Clean(request.GeneralInstructions);
        p.FollowUpDate = request.FollowUpDate;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await revisions.AddAsync(ClinicalEntityType.Prescription, p.Id, p.ConsultationId, changeType, request.AmendmentReason?.Trim(), Snapshot(p), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PrescriptionDto> FinaliseAsync(long id, CancellationToken ct)
    {
        var p = await db.Prescriptions.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Prescription", id);
        currentUser.EnsureOwnsClinicalRecord(p.DoctorId);
        p.Finalise(clock.UtcNow);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await revisions.AddAsync(ClinicalEntityType.Prescription, p.Id, p.ConsultationId, RevisionChangeType.Finalised, null, Snapshot(p), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<PrescriptionPrintDto> GetPrintAsync(long id, CancellationToken ct)
    {
        var p = await db.Prescriptions.AsNoTracking()
                    .Include(x => x.Items)
                    .Include(x => x.Patient)
                    .Include(x => x.Doctor).ThenInclude(d => d.Department)
                    .Include(x => x.Consultation).ThenInclude(c => c.Vitals)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Prescription", id);

        var v = p.Consultation.Vitals.OrderByDescending(x => x.RecordedAt).FirstOrDefault();
        var h = hospital.Value;
        return new PrescriptionPrintDto(
            new PrintHospitalDto(h.Name, h.AddressLine, h.Phone),
            new PrintDoctorDto(p.Doctor.FullName, p.Doctor.Qualification, p.Doctor.RegistrationNumber, p.Doctor.Department.Name),
            new PrintPatientDto(p.Patient.FullName, p.Patient.Mrn, p.Patient.AgeOn(clock.Today), p.Patient.Gender, p.Patient.Phone),
            p.PrescriptionNumber, p.FinalisedAt ?? p.CreatedAt, p.Status,
            v is null ? null : new PrintVitalsDto(v.TemperatureC, v.PulseBpm, v.BpSystolic, v.BpDiastolic, v.Spo2Percent, v.WeightKg),
            p.Consultation.ChiefComplaint, p.Consultation.Diagnosis, ToItemDtos(p.Items), p.GeneralInstructions, p.FollowUpDate);
    }

    public async Task<IReadOnlyList<RevisionDto>> GetHistoryAsync(long id, CancellationToken ct)
    {
        var consultationId = await db.Prescriptions.Where(p => p.Id == id).Select(p => (long?)p.ConsultationId).FirstOrDefaultAsync(ct)
                             ?? throw new NotFoundException("Prescription", id);
        return await revisions.ListAsync(consultationId, ClinicalEntityType.Prescription, id, ct);
    }

    public async Task<IReadOnlyList<PrescriptionListItemDto>> ListForPatientAsync(long patientId, CancellationToken ct) =>
        await db.Prescriptions.AsNoTracking().Where(p => p.PatientId == patientId).OrderByDescending(p => p.CreatedAt)
            .Select(p => new PrescriptionListItemDto(p.Id, p.PrescriptionNumber, p.ConsultationId,
                "Dr. " + p.Doctor.FirstName + " " + p.Doctor.LastName, p.CreatedAt, p.Status, p.Items.Count, p.FollowUpDate))
            .ToListAsync(ct);

    private async Task<List<PrescriptionItem>> BuildItemsAsync(IReadOnlyList<PrescriptionItemDto> items, CancellationToken ct)
    {
        var ids = items.Where(i => i.MedicineId.HasValue).Select(i => i.MedicineId!.Value).Distinct().ToList();
        var found = await db.Medicines.CountAsync(m => ids.Contains(m.Id), ct);
        if (found != ids.Count)
            throw new BusinessRuleException("UNKNOWN_MEDICINE", "One or more selected medicines do not exist.");

        return items.Select((i, idx) => new PrescriptionItem
        {
            MedicineId = i.MedicineId, MedicineName = i.MedicineName.Trim(), Dosage = i.Dosage.Trim(), Route = Clean(i.Route),
            FrequencyCode = i.FrequencyCode, FrequencyText = Clean(i.FrequencyText), DurationValue = i.DurationValue,
            DurationUnit = i.DurationUnit, Quantity = Clean(i.Quantity), Instructions = Clean(i.Instructions), SortOrder = (short)idx,
        }).ToList();
    }

    /// <summary>Non-blocking: flags items whose name or generic name mentions a recorded allergen (or vice versa).</summary>
    public static IReadOnlyList<string> AllergyWarnings(
        IEnumerable<string> allergens, IEnumerable<PrescriptionItem> items, IReadOnlyDictionary<long, string?> genericNames)
    {
        var warnings = new List<string>();
        var allergenList = allergens.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        foreach (var item in items)
        {
            var names = new List<string> { item.MedicineName };
            if (item.MedicineId is { } mid && genericNames.GetValueOrDefault(mid) is { } g) names.Add(g);
            foreach (var allergen in allergenList)
            {
                if (names.Any(n => n.Contains(allergen, StringComparison.OrdinalIgnoreCase) ||
                                   allergen.Contains(n, StringComparison.OrdinalIgnoreCase)))
                    warnings.Add($"{item.MedicineName}: patient has a recorded allergy to {allergen}.");
            }
        }
        return warnings;
    }

    private static List<PrescriptionItemDto> ToItemDtos(IEnumerable<PrescriptionItem> items) =>
        items.OrderBy(i => i.SortOrder).Select(i => new PrescriptionItemDto(i.MedicineId, i.MedicineName, i.Dosage, i.Route,
            i.FrequencyCode, i.FrequencyText, i.DurationValue, i.DurationUnit, i.Quantity, i.Instructions)).ToList();

    private static PrescriptionSnapshot Snapshot(Prescription p) =>
        new(p.Status, p.GeneralInstructions, p.FollowUpDate, ToItemDtos(p.Items));

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
