using HMS.Application.Common;
using HMS.Domain.Appointments;
using HMS.Domain.Billing;
using HMS.Domain.Clinical;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Identity;
using HMS.Domain.Patients;
using HMS.Domain.Prescriptions;
using Microsoft.EntityFrameworkCore;

namespace HMS.Infrastructure.Persistence;

public sealed class HmsDbContext(DbContextOptions<HmsDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Specialization> Specializations => Set<Specialization>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorSpecialization> DoctorSpecializations => Set<DoctorSpecialization>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<DoctorLeave> DoctorLeaves => Set<DoctorLeave>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
    public DbSet<PatientCondition> PatientConditions => Set<PatientCondition>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentStatusHistory> AppointmentStatusHistory => Set<AppointmentStatusHistory>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<ConsultationSymptom> ConsultationSymptoms => Set<ConsultationSymptom>();
    public DbSet<VitalSigns> VitalSigns => Set<VitalSigns>();
    public DbSet<ClinicalRecordRevision> ClinicalRecordRevisions => Set<ClinicalRecordRevision>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdSequence> IdSequences => Set<IdSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HmsDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(10, 2);
        b.Properties<DateTime>().HaveColumnType("datetime(6)");

        // Enums are stored as readable strings.
        b.Properties<Gender>().HaveConversion<string>().HaveMaxLength(10);
        b.Properties<Role>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<AppointmentStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<AppointmentType>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<ClinicalStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<DurationUnit>().HaveConversion<string>().HaveMaxLength(10);
        b.Properties<FrequencyCode>().HaveConversion<string>().HaveMaxLength(10);
        b.Properties<ClinicalEntityType>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<RevisionChangeType>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<InvoiceStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<PaymentMethod>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<InvoiceItemType>().HaveConversion<string>().HaveMaxLength(20);
    }
}
