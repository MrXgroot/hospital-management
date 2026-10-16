using FluentValidation;
using HMS.Application.Common;
using HMS.Domain.Clinical;
using HMS.Domain.Common;
using HMS.Domain.Prescriptions;

namespace HMS.Application.Prescriptions;

public sealed record PrescriptionItemDto(
    long? MedicineId, string MedicineName, string Dosage, string? Route, FrequencyCode FrequencyCode,
    string? FrequencyText, short? DurationValue, DurationUnit? DurationUnit, string? Quantity, string? Instructions);

public sealed record PrescriptionDto(
    long Id, string PrescriptionNumber, long ConsultationId, long PatientId, string PatientName, string PatientMrn,
    long DoctorId, string DoctorName, ClinicalStatus Status, string? GeneralInstructions, DateOnly? FollowUpDate,
    DateTime? FinalisedAt, DateTime CreatedAt, IReadOnlyList<PrescriptionItemDto> Items, int RowVersion, bool CanEdit,
    IReadOnlyList<string> AllergyWarnings);

public sealed record PrescriptionListItemDto(
    long Id, string PrescriptionNumber, long ConsultationId, string DoctorName, DateTime CreatedAt,
    ClinicalStatus Status, int ItemCount, DateOnly? FollowUpDate);

public sealed record SavePrescriptionRequest(
    string? GeneralInstructions, DateOnly? FollowUpDate, IReadOnlyList<PrescriptionItemDto> Items,
    int? RowVersion, string? AmendmentReason);

public sealed record PrescriptionSnapshot(
    ClinicalStatus Status, string? GeneralInstructions, DateOnly? FollowUpDate, IReadOnlyList<PrescriptionItemDto> Items);

public sealed record PrintHospitalDto(string Name, string AddressLine, string Phone);
public sealed record PrintDoctorDto(string Name, string Qualification, string RegistrationNumber, string Department);
public sealed record PrintPatientDto(string Name, string Mrn, int? Age, Gender Gender, string Phone);
public sealed record PrintVitalsDto(decimal? TemperatureC, short? PulseBpm, short? BpSystolic, short? BpDiastolic, byte? Spo2Percent, decimal? WeightKg);

public sealed record PrescriptionPrintDto(
    PrintHospitalDto Hospital, PrintDoctorDto Doctor, PrintPatientDto Patient, string PrescriptionNumber,
    DateTime Date, ClinicalStatus Status, PrintVitalsDto? Vitals, string? ChiefComplaint, string? Diagnosis,
    IReadOnlyList<PrescriptionItemDto> Items, string? GeneralInstructions, DateOnly? FollowUpDate);

public sealed class SavePrescriptionRequestValidator : AbstractValidator<SavePrescriptionRequest>
{
    public SavePrescriptionRequestValidator(IClock clock)
    {
        RuleFor(x => x.GeneralInstructions).MaximumLength(5_000);
        RuleFor(x => x.FollowUpDate).GreaterThan(_ => clock.Today).When(x => x.FollowUpDate.HasValue)
            .WithMessage("Follow-up date must be in the future.");
        RuleFor(x => x.Items).NotNull();
        RuleFor(x => x.AmendmentReason).MaximumLength(500);
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.MedicineName).NotEmpty().MaximumLength(150);
            i.RuleFor(x => x.Dosage).NotEmpty().MaximumLength(50);
            i.RuleFor(x => x.Route).MaximumLength(20);
            i.RuleFor(x => x.FrequencyCode).IsInEnum();
            i.RuleFor(x => x.FrequencyText).NotEmpty().When(x => x.FrequencyCode == FrequencyCode.CUSTOM)
                .WithMessage("Describe the custom frequency.");
            i.RuleFor(x => x.FrequencyText).MaximumLength(60);
            i.RuleFor(x => x.DurationValue).InclusiveBetween((short)1, (short)999).When(x => x.DurationValue.HasValue);
            i.RuleFor(x => x.DurationUnit).NotNull().When(x => x.DurationValue.HasValue).WithMessage("Select a duration unit.");
            i.RuleFor(x => x.Quantity).MaximumLength(30);
            i.RuleFor(x => x.Instructions).MaximumLength(300);
        });
    }
}
