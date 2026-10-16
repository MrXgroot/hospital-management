using HMS.Application.Common;
using HMS.Domain.Common;
using HMS.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Patients;

public interface IPatientService
{
    Task<PagedResult<PatientListItemDto>> SearchAsync(string? search, PageQuery page, CancellationToken ct);
    Task<PatientDto> GetAsync(long id, CancellationToken ct);
    Task<PatientDto> CreateAsync(SavePatientRequest request, bool confirmDuplicate, CancellationToken ct);
    Task<PatientDto> UpdateAsync(long id, SavePatientRequest request, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
    Task<PagedResult<VisitDto>> GetVisitsAsync(long id, PageQuery page, CancellationToken ct);
    Task<AllergyDto> SaveAllergyAsync(long patientId, long? allergyId, SaveAllergyRequest request, CancellationToken ct);
    Task DeleteAllergyAsync(long patientId, long allergyId, CancellationToken ct);
    Task<ConditionDto> SaveConditionAsync(long patientId, long? conditionId, SaveConditionRequest request, CancellationToken ct);
    Task DeleteConditionAsync(long patientId, long conditionId, CancellationToken ct);
}

public sealed class PatientService(IAppDbContext db, IClock clock, ISequenceGenerator sequences) : IPatientService
{
    public async Task<PagedResult<PatientListItemDto>> SearchAsync(string? search, PageQuery page, CancellationToken ct)
    {
        var q = db.Patients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            if (IdFormats.LooksLikeMrn(s))
                q = q.Where(p => p.Mrn == s.ToUpperInvariant());
            else if (s.TrimStart('+').All(char.IsDigit))
            {
                var phone = Patient.NormalisePhone(s);
                q = q.Where(p => p.Phone.StartsWith(phone));
            }
            else
            {
                var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string first = parts[0], last = parts[^1];
                    q = q.Where(p => p.FirstName.StartsWith(first) && p.LastName.StartsWith(last));
                }
                else
                    q = q.Where(p => p.FirstName.StartsWith(s) || p.LastName.StartsWith(s));
            }
        }

        var today = clock.Today;
        var result = await q.OrderByDescending(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Mrn, p.FirstName, p.LastName, p.Gender, p.DateOfBirth, p.AgeYears, p.AgeRecordedOn, p.Phone, p.City, p.CreatedAt })
            .ToPagedAsync(page, ct);

        return new PagedResult<PatientListItemDto>(
            result.Items.Select(p => new PatientListItemDto(p.Id, p.Mrn, $"{p.FirstName} {p.LastName}", p.Gender,
                Patient.CalculateAge(p.DateOfBirth, p.AgeYears, p.AgeRecordedOn, today), p.Phone, p.City, p.CreatedAt)).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<PatientDto> GetAsync(long id, CancellationToken ct)
    {
        var p = await db.Patients.AsNoTracking().Include(x => x.Allergies).Include(x => x.Conditions)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Patient", id);
        return ToDto(p);
    }

    public async Task<PatientDto> CreateAsync(SavePatientRequest request, bool confirmDuplicate, CancellationToken ct)
    {
        var phone = Patient.NormalisePhone(request.Phone);
        if (!confirmDuplicate)
        {
            var firstName = request.FirstName.Trim();
            var today = clock.Today;
            var matches = (await db.Patients.AsNoTracking()
                    .Where(p => p.Phone == phone && p.FirstName == firstName)
                    .Take(10).ToListAsync(ct))
                .Select(p => new DuplicatePatientMatch(p.Id, p.Mrn, p.FullName, p.Phone, p.AgeOn(today))).ToList();
            if (matches.Count > 0)
                throw new ConflictException("DUPLICATE_PATIENT",
                    "A patient with the same first name and phone number already exists.").With("matches", matches);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var year = clock.Today.Year;
        var patient = new Patient { Mrn = IdFormats.Mrn(year, await sequences.NextAsync(IdFormats.PatientSequenceKey(year), ct)) };
        Apply(patient, request);
        db.Patients.Add(patient);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(patient);
    }

    public async Task<PatientDto> UpdateAsync(long id, SavePatientRequest request, CancellationToken ct)
    {
        var patient = await db.Patients.Include(p => p.Allergies).Include(p => p.Conditions)
                          .FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Patient", id);
        Apply(patient, request);
        await db.SaveChangesAsync(ct);
        return ToDto(patient);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Patient", id);
        if (await db.Consultations.AnyAsync(c => c.PatientId == id, ct))
            throw new BusinessRuleException("PATIENT_HAS_CONSULTATIONS", "Patients with medical records cannot be deleted.");
        patient.IsDeleted = true;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<VisitDto>> GetVisitsAsync(long id, PageQuery page, CancellationToken ct)
    {
        await EnsureExistsAsync(id, ct);
        return await db.Consultations.AsNoTracking().Where(c => c.PatientId == id)
            .OrderByDescending(c => c.StartedAt)
            .Select(c => new VisitDto(c.Id, c.StartedAt, c.DoctorId, "Dr. " + c.Doctor.FirstName + " " + c.Doctor.LastName,
                c.ChiefComplaint, c.Diagnosis, c.Status, c.Prescription != null ? c.Prescription.Id : null))
            .ToPagedAsync(page, ct);
    }

    public async Task<AllergyDto> SaveAllergyAsync(long patientId, long? allergyId, SaveAllergyRequest request, CancellationToken ct)
    {
        await EnsureExistsAsync(patientId, ct);
        var allergy = allergyId is null
            ? db.PatientAllergies.Add(new PatientAllergy { PatientId = patientId }).Entity
            : await db.PatientAllergies.FirstOrDefaultAsync(a => a.Id == allergyId && a.PatientId == patientId, ct)
              ?? throw new NotFoundException("Allergy", allergyId);
        allergy.Allergen = request.Allergen.Trim();
        allergy.Reaction = request.Reaction;
        allergy.Severity = request.Severity;
        await db.SaveChangesAsync(ct);
        return new AllergyDto(allergy.Id, allergy.Allergen, allergy.Reaction, allergy.Severity);
    }

    public async Task DeleteAllergyAsync(long patientId, long allergyId, CancellationToken ct)
    {
        var allergy = await db.PatientAllergies.FirstOrDefaultAsync(a => a.Id == allergyId && a.PatientId == patientId, ct)
                      ?? throw new NotFoundException("Allergy", allergyId);
        db.PatientAllergies.Remove(allergy);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ConditionDto> SaveConditionAsync(long patientId, long? conditionId, SaveConditionRequest request, CancellationToken ct)
    {
        await EnsureExistsAsync(patientId, ct);
        var condition = conditionId is null
            ? db.PatientConditions.Add(new PatientCondition { PatientId = patientId }).Entity
            : await db.PatientConditions.FirstOrDefaultAsync(c => c.Id == conditionId && c.PatientId == patientId, ct)
              ?? throw new NotFoundException("Condition", conditionId);
        condition.ConditionName = request.ConditionName.Trim();
        condition.SinceYear = request.SinceYear;
        condition.Notes = request.Notes;
        await db.SaveChangesAsync(ct);
        return new ConditionDto(condition.Id, condition.ConditionName, condition.SinceYear, condition.Notes);
    }

    public async Task DeleteConditionAsync(long patientId, long conditionId, CancellationToken ct)
    {
        var condition = await db.PatientConditions.FirstOrDefaultAsync(c => c.Id == conditionId && c.PatientId == patientId, ct)
                        ?? throw new NotFoundException("Condition", conditionId);
        db.PatientConditions.Remove(condition);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureExistsAsync(long id, CancellationToken ct)
    {
        if (!await db.Patients.AnyAsync(p => p.Id == id, ct)) throw new NotFoundException("Patient", id);
    }

    private void Apply(Patient p, SavePatientRequest r)
    {
        p.FirstName = r.FirstName.Trim();
        p.LastName = r.LastName.Trim();
        p.Gender = r.Gender;
        if (r.DateOfBirth is not null)
        {
            p.DateOfBirth = r.DateOfBirth;
            p.AgeYears = null;
            p.AgeRecordedOn = null;
        }
        else if (r.AgeYears != p.AgeOn(clock.Today) || p.DateOfBirth is not null)
        {
            // Only reset the recorded date when the age actually changed, so ages keep counting up.
            p.DateOfBirth = null;
            p.AgeYears = r.AgeYears;
            p.AgeRecordedOn = clock.Today;
        }
        p.BloodGroup = string.IsNullOrWhiteSpace(r.BloodGroup) ? null : r.BloodGroup;
        p.Phone = Patient.NormalisePhone(r.Phone);
        p.AlternatePhone = string.IsNullOrWhiteSpace(r.AlternatePhone) ? null : Patient.NormalisePhone(r.AlternatePhone);
        p.Email = NullIfEmpty(r.Email);
        p.AddressLine1 = NullIfEmpty(r.AddressLine1);
        p.AddressLine2 = NullIfEmpty(r.AddressLine2);
        p.City = NullIfEmpty(r.City);
        p.State = NullIfEmpty(r.State);
        p.PostalCode = NullIfEmpty(r.PostalCode);
        p.Country = NullIfEmpty(r.Country);
        p.EmergencyContactName = NullIfEmpty(r.EmergencyContactName);
        p.EmergencyContactPhone = string.IsNullOrWhiteSpace(r.EmergencyContactPhone) ? null : Patient.NormalisePhone(r.EmergencyContactPhone);
        p.EmergencyContactRelation = NullIfEmpty(r.EmergencyContactRelation);
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private PatientDto ToDto(Patient p) => new(
        p.Id, p.Mrn, p.FirstName, p.LastName, p.FullName, p.Gender, p.DateOfBirth, p.AgeYears, p.AgeOn(clock.Today),
        p.BloodGroup, p.Phone, p.AlternatePhone, p.Email, p.AddressLine1, p.AddressLine2, p.City, p.State,
        p.PostalCode, p.Country, p.EmergencyContactName, p.EmergencyContactPhone, p.EmergencyContactRelation, p.CreatedAt,
        p.Allergies.Select(a => new AllergyDto(a.Id, a.Allergen, a.Reaction, a.Severity)).ToList(),
        p.Conditions.Select(c => new ConditionDto(c.Id, c.ConditionName, c.SinceYear, c.Notes)).ToList());
}
