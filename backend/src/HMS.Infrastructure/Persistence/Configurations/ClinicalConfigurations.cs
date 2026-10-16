using HMS.Domain.Clinical;
using HMS.Domain.Prescriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HMS.Infrastructure.Persistence.Configurations;

public sealed class ConsultationConfiguration : IEntityTypeConfiguration<Consultation>
{
    public void Configure(EntityTypeBuilder<Consultation> b)
    {
        b.Property(x => x.ChiefComplaint).HasMaxLength(500);
        b.Property(x => x.HistoryOfPresentIllness).HasColumnType("text");
        b.Property(x => x.ExaminationFindings).HasColumnType("text");
        b.Property(x => x.Diagnosis).HasMaxLength(500);
        b.Property(x => x.DiagnosisIcd10).HasMaxLength(10);
        b.Property(x => x.ClinicalNotes).HasColumnType("text");
        b.Property(x => x.Advice).HasColumnType("text");
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => x.AppointmentId).IsUnique().HasDatabaseName("UX_consultations_appointment_id");
        b.HasIndex(x => new { x.PatientId, x.StartedAt }).HasDatabaseName("IX_consultations_patient_id_started_at");
        b.HasIndex(x => new { x.DoctorId, x.StartedAt }).HasDatabaseName("IX_consultations_doctor_id_started_at");
        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Symptoms).WithOne().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Vitals).WithOne().HasForeignKey(x => x.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Prescription).WithOne(p => p.Consultation).HasForeignKey<Prescription>(p => p.ConsultationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ConsultationSymptomConfiguration : IEntityTypeConfiguration<ConsultationSymptom>
{
    public void Configure(EntityTypeBuilder<ConsultationSymptom> b)
    {
        b.Property(x => x.Symptom).HasMaxLength(150);
        b.Property(x => x.Severity).HasMaxLength(20);
        b.HasIndex(x => x.ConsultationId).HasDatabaseName("IX_consultation_symptoms_consultation_id");
    }
}

public sealed class VitalSignsConfiguration : IEntityTypeConfiguration<VitalSigns>
{
    public void Configure(EntityTypeBuilder<VitalSigns> b)
    {
        b.Property(x => x.TemperatureC).HasPrecision(4, 1);
        b.Property(x => x.WeightKg).HasPrecision(5, 2);
        b.Property(x => x.HeightCm).HasPrecision(5, 1);
        b.Property(x => x.Bmi).HasPrecision(4, 1);
        b.Property(x => x.Notes).HasMaxLength(300);
        b.HasIndex(x => x.ConsultationId).HasDatabaseName("IX_vital_signs_consultation_id");
        b.HasIndex(x => new { x.PatientId, x.RecordedAt }).HasDatabaseName("IX_vital_signs_patient_id_recorded_at");
        b.HasOne<HMS.Domain.Patients.Patient>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClinicalRecordRevisionConfiguration : IEntityTypeConfiguration<ClinicalRecordRevision>
{
    public void Configure(EntityTypeBuilder<ClinicalRecordRevision> b)
    {
        b.Property(x => x.Reason).HasMaxLength(500);
        b.Property(x => x.SnapshotJson).HasColumnType("json");
        b.Property(x => x.ChangedFields).HasColumnType("json");
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.RevisionNumber }).IsUnique().HasDatabaseName("UX_clinical_record_revisions_entity_rev");
        b.HasIndex(x => x.ConsultationId).HasDatabaseName("IX_clinical_record_revisions_consultation_id");
    }
}

public sealed class MedicineConfiguration : IEntityTypeConfiguration<Medicine>
{
    public void Configure(EntityTypeBuilder<Medicine> b)
    {
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.GenericName).HasMaxLength(150);
        b.Property(x => x.Form).HasMaxLength(30);
        b.Property(x => x.Strength).HasMaxLength(50);
        b.HasIndex(x => x.Name).HasDatabaseName("IX_medicines_name");
        b.HasIndex(x => x.GenericName).HasDatabaseName("IX_medicines_generic_name");
    }
}

public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> b)
    {
        b.Property(x => x.PrescriptionNumber).HasMaxLength(16);
        b.Property(x => x.GeneralInstructions).HasColumnType("text");
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => x.PrescriptionNumber).IsUnique().HasDatabaseName("UX_prescriptions_prescription_number");
        b.HasIndex(x => x.ConsultationId).IsUnique().HasDatabaseName("UX_prescriptions_consultation_id");
        b.HasIndex(x => x.PatientId).HasDatabaseName("IX_prescriptions_patient_id");
        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> b)
    {
        b.Property(x => x.MedicineName).HasMaxLength(150);
        b.Property(x => x.Dosage).HasMaxLength(50);
        b.Property(x => x.Route).HasMaxLength(20);
        b.Property(x => x.FrequencyText).HasMaxLength(60);
        b.Property(x => x.Quantity).HasMaxLength(30);
        b.Property(x => x.Instructions).HasMaxLength(300);
        b.HasIndex(x => x.PrescriptionId).HasDatabaseName("IX_prescription_items_prescription_id");
        b.HasOne<Medicine>().WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
    }
}
