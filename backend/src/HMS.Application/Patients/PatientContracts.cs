using FluentValidation;
using HMS.Application.Common;
using HMS.Domain.Clinical;
using HMS.Domain.Common;

namespace HMS.Application.Patients;

public sealed record PatientListItemDto(
    long Id, string Mrn, string FullName, Gender Gender, int? Age, string Phone, string? City, DateTime CreatedAt);

public sealed record AllergyDto(long Id, string Allergen, string? Reaction, string? Severity);
public sealed record ConditionDto(long Id, string ConditionName, short? SinceYear, string? Notes);

public sealed record PatientDto(
    long Id, string Mrn, string FirstName, string LastName, string FullName, Gender Gender,
    DateOnly? DateOfBirth, short? AgeYears, int? Age, string? BloodGroup,
    string Phone, string? AlternatePhone, string? Email,
    string? AddressLine1, string? AddressLine2, string? City, string? State, string? PostalCode, string? Country,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelation,
    DateTime CreatedAt, IReadOnlyList<AllergyDto> Allergies, IReadOnlyList<ConditionDto> Conditions);

public sealed record SavePatientRequest(
    string FirstName, string LastName, Gender Gender, DateOnly? DateOfBirth, short? AgeYears, string? BloodGroup,
    string Phone, string? AlternatePhone, string? Email,
    string? AddressLine1, string? AddressLine2, string? City, string? State, string? PostalCode, string? Country,
    string? EmergencyContactName, string? EmergencyContactPhone, string? EmergencyContactRelation);

public sealed record SaveAllergyRequest(string Allergen, string? Reaction, string? Severity);
public sealed record SaveConditionRequest(string ConditionName, short? SinceYear, string? Notes);

public sealed record VisitDto(
    long ConsultationId, DateTime StartedAt, long DoctorId, string DoctorName, string? ChiefComplaint,
    string? Diagnosis, ClinicalStatus Status, long? PrescriptionId);

public sealed record DuplicatePatientMatch(long Id, string Mrn, string FullName, string Phone, int? Age);

public sealed class SavePatientRequestValidator : AbstractValidator<SavePatientRequest>
{
    private static readonly string[] BloodGroups = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];
    private const string PhonePattern = @"^\+?[0-9 -]{10,20}$";

    public SavePatientRequestValidator(IClock clock)
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Gender).IsInEnum();
        RuleFor(x => x).Must(x => x.DateOfBirth.HasValue || x.AgeYears.HasValue)
            .WithName(nameof(SavePatientRequest.DateOfBirth)).WithMessage("Enter a date of birth or an age.");
        RuleFor(x => x.DateOfBirth).Must(d => d!.Value <= clock.Today && d.Value.Year >= 1900)
            .When(x => x.DateOfBirth.HasValue).WithMessage("Date of birth must be a past date.");
        RuleFor(x => x.AgeYears).InclusiveBetween((short)0, (short)130).When(x => x.AgeYears.HasValue);
        RuleFor(x => x.BloodGroup).Must(b => BloodGroups.Contains(b)).When(x => !string.IsNullOrEmpty(x.BloodGroup))
            .WithMessage("Invalid blood group.");
        RuleFor(x => x.Phone).NotEmpty().Matches(PhonePattern).WithMessage("Enter a valid phone number (10-15 digits).");
        RuleFor(x => x.AlternatePhone).Matches(PhonePattern).When(x => !string.IsNullOrEmpty(x.AlternatePhone));
        RuleFor(x => x.EmergencyContactPhone).Matches(PhonePattern).When(x => !string.IsNullOrEmpty(x.EmergencyContactPhone));
        RuleFor(x => x.Email).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.AddressLine1).MaximumLength(200);
        RuleFor(x => x.AddressLine2).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(80);
        RuleFor(x => x.State).MaximumLength(80);
        RuleFor(x => x.PostalCode).MaximumLength(12);
        RuleFor(x => x.Country).MaximumLength(60);
        RuleFor(x => x.EmergencyContactName).MaximumLength(150);
        RuleFor(x => x.EmergencyContactRelation).MaximumLength(40);
    }
}

public sealed class SaveAllergyRequestValidator : AbstractValidator<SaveAllergyRequest>
{
    public SaveAllergyRequestValidator()
    {
        RuleFor(x => x.Allergen).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Reaction).MaximumLength(200);
        RuleFor(x => x.Severity).Must(s => s is "Mild" or "Moderate" or "Severe")
            .When(x => !string.IsNullOrEmpty(x.Severity)).WithMessage("Severity must be Mild, Moderate or Severe.");
    }
}

public sealed class SaveConditionRequestValidator : AbstractValidator<SaveConditionRequest>
{
    public SaveConditionRequestValidator()
    {
        RuleFor(x => x.ConditionName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.SinceYear).InclusiveBetween((short)1900, (short)2100).When(x => x.SinceYear.HasValue);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
