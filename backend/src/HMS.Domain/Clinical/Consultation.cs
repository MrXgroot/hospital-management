using HMS.Domain.Appointments;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Patients;
using HMS.Domain.Prescriptions;

namespace HMS.Domain.Clinical;

public enum ClinicalStatus { Draft, Finalised }

public class Consultation : AuditableEntity, IVersioned
{
    public long PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public long DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public long? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public ClinicalStatus Status { get; set; } = ClinicalStatus.Draft;
    public DateTime StartedAt { get; set; }
    public DateTime? FinalisedAt { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? HistoryOfPresentIllness { get; set; }
    public string? ExaminationFindings { get; set; }
    public string? Diagnosis { get; set; }
    public string? DiagnosisIcd10 { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? Advice { get; set; }
    public int RowVersion { get; set; }

    public List<ConsultationSymptom> Symptoms { get; set; } = [];
    public List<VitalSigns> Vitals { get; set; } = [];
    public Prescription? Prescription { get; set; }

    public void Finalise(DateTime utcNow)
    {
        if (Status == ClinicalStatus.Finalised)
            throw new BusinessRuleException("CONSULTATION_FINALISED", "The consultation is already finalised.");
        if (string.IsNullOrWhiteSpace(ChiefComplaint))
            throw new BusinessRuleException("CHIEF_COMPLAINT_REQUIRED", "A chief complaint is required to finalise.");
        Status = ClinicalStatus.Finalised;
        FinalisedAt = utcNow;
    }
}

public class ConsultationSymptom : Entity
{
    public long ConsultationId { get; set; }
    public string Symptom { get; set; } = "";
    public short? DurationValue { get; set; }
    public DurationUnit? DurationUnit { get; set; }
    public string? Severity { get; set; }
    public short SortOrder { get; set; }
}

public class VitalSigns : Entity
{
    public long ConsultationId { get; set; }
    public long PatientId { get; set; }
    public DateTime RecordedAt { get; set; }
    public long RecordedBy { get; set; }
    public decimal? TemperatureC { get; set; }
    public short? PulseBpm { get; set; }
    public short? BpSystolic { get; set; }
    public short? BpDiastolic { get; set; }
    public short? RespiratoryRate { get; set; }
    public byte? Spo2Percent { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? Bmi { get; set; }
    public string? Notes { get; set; }

    public void ComputeBmi() => Bmi = CalculateBmi(WeightKg, HeightCm);

    public static decimal? CalculateBmi(decimal? weightKg, decimal? heightCm)
    {
        if (weightKg is not > 0 || heightCm is not > 0) return null;
        var m = heightCm.Value / 100m;
        return Math.Round(weightKg.Value / (m * m), 1);
    }

    /// <summary>Non-blocking warnings for values outside normal adult ranges.</summary>
    public IReadOnlyList<string> AbnormalWarnings()
    {
        var w = new List<string>();
        if (TemperatureC is { } t && (t < 36.0m || t > 37.5m)) w.Add($"Temperature {t} °C is outside 36.0–37.5");
        if (PulseBpm is { } p && (p < 60 || p > 100)) w.Add($"Pulse {p} bpm is outside 60–100");
        if (BpSystolic is { } s && (s < 90 || s > 140)) w.Add($"Systolic BP {s} mmHg is outside 90–140");
        if (BpDiastolic is { } d && (d < 60 || d > 90)) w.Add($"Diastolic BP {d} mmHg is outside 60–90");
        if (RespiratoryRate is { } r && (r < 12 || r > 20)) w.Add($"Respiratory rate {r} is outside 12–20");
        if (Spo2Percent is { } o && o < 95) w.Add($"SpO2 {o}% is below 95");
        if (Bmi is { } b && (b < 18.5m || b >= 25m)) w.Add($"BMI {b} is outside 18.5–24.9");
        return w;
    }
}

public enum ClinicalEntityType { Consultation, Prescription, VitalSigns }

public enum RevisionChangeType { Created, Updated, Finalised, Amended }

/// <summary>Append-only history of clinical records. Never updated or deleted.</summary>
public class ClinicalRecordRevision : Entity
{
    public ClinicalEntityType EntityType { get; set; }
    public long EntityId { get; set; }
    public long ConsultationId { get; set; }
    public int RevisionNumber { get; set; }
    public RevisionChangeType ChangeType { get; set; }
    public string? Reason { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string? ChangedFields { get; set; }
    public DateTime ChangedAt { get; set; }
    public long ChangedBy { get; set; }
}
