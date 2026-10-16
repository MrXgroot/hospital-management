using HMS.Domain.Clinical;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Patients;

namespace HMS.Domain.Prescriptions;

public enum FrequencyCode { OD, BD, TDS, QID, HS, SOS, STAT, CUSTOM }

public class Medicine : AuditableEntity
{
    public string Name { get; set; } = "";
    public string? GenericName { get; set; }
    public string? Form { get; set; }
    public string? Strength { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Prescription : AuditableEntity, IVersioned
{
    public string PrescriptionNumber { get; set; } = "";
    public long ConsultationId { get; set; }
    public Consultation Consultation { get; set; } = null!;
    public long PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public long DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public ClinicalStatus Status { get; set; } = ClinicalStatus.Draft;
    public string? GeneralInstructions { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public DateTime? FinalisedAt { get; set; }
    public int RowVersion { get; set; }

    public List<PrescriptionItem> Items { get; set; } = [];

    public void Finalise(DateTime utcNow)
    {
        if (Status == ClinicalStatus.Finalised)
            throw new BusinessRuleException("PRESCRIPTION_FINALISED", "The prescription is already finalised.");
        if (Items.Count == 0)
            throw new BusinessRuleException("PRESCRIPTION_EMPTY", "Add at least one medicine before finalising.");
        Status = ClinicalStatus.Finalised;
        FinalisedAt = utcNow;
    }
}

public class PrescriptionItem : Entity
{
    public long PrescriptionId { get; set; }
    public long? MedicineId { get; set; }
    public string MedicineName { get; set; } = "";
    public string Dosage { get; set; } = "";
    public string? Route { get; set; }
    public FrequencyCode FrequencyCode { get; set; }
    public string? FrequencyText { get; set; }
    public short? DurationValue { get; set; }
    public DurationUnit? DurationUnit { get; set; }
    public string? Quantity { get; set; }
    public string? Instructions { get; set; }
    public short SortOrder { get; set; }
}
