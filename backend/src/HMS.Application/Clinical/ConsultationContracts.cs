using FluentValidation;
using HMS.Domain.Clinical;
using HMS.Domain.Common;

namespace HMS.Application.Clinical;

public sealed record SymptomDto(string Symptom, short? DurationValue, DurationUnit? DurationUnit, string? Severity);

public sealed record VitalSignsDto(
    long Id, DateTime RecordedAt, string? RecordedByName, decimal? TemperatureC, short? PulseBpm,
    short? BpSystolic, short? BpDiastolic, short? RespiratoryRate, byte? Spo2Percent,
    decimal? WeightKg, decimal? HeightCm, decimal? Bmi, string? Notes, IReadOnlyList<string> Warnings);

public sealed record ConsultationPatientDto(
    long Id, string Mrn, string FullName, Gender Gender, int? Age, string? BloodGroup,
    IReadOnlyList<string> Allergies, IReadOnlyList<string> Conditions);

public sealed record ConsultationDto(
    long Id, ConsultationPatientDto Patient, long DoctorId, string DoctorName, long? AppointmentId,
    ClinicalStatus Status, DateTime StartedAt, DateTime? FinalisedAt,
    string? ChiefComplaint, string? HistoryOfPresentIllness, string? ExaminationFindings,
    string? Diagnosis, string? DiagnosisIcd10, string? ClinicalNotes, string? Advice,
    IReadOnlyList<SymptomDto> Symptoms, IReadOnlyList<VitalSignsDto> Vitals,
    long? PrescriptionId, int RowVersion, bool CanEdit);

public sealed record StartConsultationRequest(long? AppointmentId, long? PatientId);

public sealed record UpdateConsultationRequest(
    string? ChiefComplaint, string? HistoryOfPresentIllness, string? ExaminationFindings,
    string? Diagnosis, string? DiagnosisIcd10, string? ClinicalNotes, string? Advice,
    IReadOnlyList<SymptomDto> Symptoms, int RowVersion, string? AmendmentReason);

public sealed record SaveVitalsRequest(
    decimal? TemperatureC, short? PulseBpm, short? BpSystolic, short? BpDiastolic, short? RespiratoryRate,
    byte? Spo2Percent, decimal? WeightKg, decimal? HeightCm, string? Notes, string? AmendmentReason);

/// <summary>What gets stored in clinical_record_revisions for a consultation.</summary>
public sealed record ConsultationSnapshot(
    ClinicalStatus Status, string? ChiefComplaint, string? HistoryOfPresentIllness, string? ExaminationFindings,
    string? Diagnosis, string? DiagnosisIcd10, string? ClinicalNotes, string? Advice, IReadOnlyList<SymptomDto> Symptoms);

public sealed record VitalsSnapshot(
    decimal? TemperatureC, short? PulseBpm, short? BpSystolic, short? BpDiastolic, short? RespiratoryRate,
    byte? Spo2Percent, decimal? WeightKg, decimal? HeightCm, decimal? Bmi, string? Notes);

public sealed class StartConsultationRequestValidator : AbstractValidator<StartConsultationRequest>
{
    public StartConsultationRequestValidator() =>
        RuleFor(x => x).Must(x => x.AppointmentId.HasValue ^ x.PatientId.HasValue)
            .WithName("appointmentId").WithMessage("Provide either an appointment or a patient (walk-in).");
}

public sealed class UpdateConsultationRequestValidator : AbstractValidator<UpdateConsultationRequest>
{
    public UpdateConsultationRequestValidator()
    {
        RuleFor(x => x.ChiefComplaint).MaximumLength(500);
        RuleFor(x => x.Diagnosis).MaximumLength(500);
        RuleFor(x => x.DiagnosisIcd10).MaximumLength(10).Matches(@"^[A-Za-z][0-9][0-9A-Za-z](\.[0-9A-Za-z]{1,4})?$")
            .When(x => !string.IsNullOrEmpty(x.DiagnosisIcd10)).WithMessage("Enter a valid ICD-10 code (e.g. J06.9).");
        RuleFor(x => x.HistoryOfPresentIllness).MaximumLength(10_000);
        RuleFor(x => x.ExaminationFindings).MaximumLength(10_000);
        RuleFor(x => x.ClinicalNotes).MaximumLength(10_000);
        RuleFor(x => x.Advice).MaximumLength(10_000);
        RuleFor(x => x.AmendmentReason).MaximumLength(500);
        RuleForEach(x => x.Symptoms).ChildRules(s =>
        {
            s.RuleFor(x => x.Symptom).NotEmpty().MaximumLength(150);
            s.RuleFor(x => x.DurationValue).InclusiveBetween((short)0, (short)999).When(x => x.DurationValue.HasValue);
            s.RuleFor(x => x.DurationUnit).IsInEnum().When(x => x.DurationUnit.HasValue);
            s.RuleFor(x => x.Severity).MaximumLength(20);
        });
    }
}

/// <summary>Rejects physiologically impossible values (likely typos). Abnormal-but-possible values only warn.</summary>
public sealed class SaveVitalsRequestValidator : AbstractValidator<SaveVitalsRequest>
{
    public SaveVitalsRequestValidator()
    {
        RuleFor(x => x).Must(HasAnyValue).WithName("vitals").WithMessage("Enter at least one vital sign.");
        RuleFor(x => x.TemperatureC).InclusiveBetween(30m, 45m).When(x => x.TemperatureC.HasValue);
        RuleFor(x => x.PulseBpm).InclusiveBetween((short)20, (short)250).When(x => x.PulseBpm.HasValue);
        RuleFor(x => x.BpSystolic).InclusiveBetween((short)50, (short)260).When(x => x.BpSystolic.HasValue);
        RuleFor(x => x.BpDiastolic).InclusiveBetween((short)30, (short)160).When(x => x.BpDiastolic.HasValue);
        RuleFor(x => x).Must(x => x.BpDiastolic < x.BpSystolic)
            .When(x => x.BpSystolic.HasValue && x.BpDiastolic.HasValue)
            .WithName(nameof(SaveVitalsRequest.BpDiastolic)).WithMessage("Diastolic must be lower than systolic.");
        RuleFor(x => x.RespiratoryRate).InclusiveBetween((short)4, (short)70).When(x => x.RespiratoryRate.HasValue);
        RuleFor(x => x.Spo2Percent).InclusiveBetween((byte)50, (byte)100).When(x => x.Spo2Percent.HasValue);
        RuleFor(x => x.WeightKg).InclusiveBetween(0.3m, 400m).When(x => x.WeightKg.HasValue);
        RuleFor(x => x.HeightCm).InclusiveBetween(20m, 250m).When(x => x.HeightCm.HasValue);
        RuleFor(x => x.Notes).MaximumLength(300);
        RuleFor(x => x.AmendmentReason).MaximumLength(500);
    }

    private static bool HasAnyValue(SaveVitalsRequest x) =>
        x.TemperatureC.HasValue || x.PulseBpm.HasValue || x.BpSystolic.HasValue || x.BpDiastolic.HasValue ||
        x.RespiratoryRate.HasValue || x.Spo2Percent.HasValue || x.WeightKg.HasValue || x.HeightCm.HasValue;
}
