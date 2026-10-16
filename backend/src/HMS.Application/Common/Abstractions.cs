using HMS.Domain.Appointments;
using HMS.Domain.Billing;
using HMS.Domain.Clinical;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Identity;
using HMS.Domain.Patients;
using HMS.Domain.Prescriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace HMS.Application.Common;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Department> Departments { get; }
    DbSet<Specialization> Specializations { get; }
    DbSet<Doctor> Doctors { get; }
    DbSet<DoctorSpecialization> DoctorSpecializations { get; }
    DbSet<DoctorSchedule> DoctorSchedules { get; }
    DbSet<DoctorLeave> DoctorLeaves { get; }
    DbSet<Patient> Patients { get; }
    DbSet<PatientAllergy> PatientAllergies { get; }
    DbSet<PatientCondition> PatientConditions { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<AppointmentStatusHistory> AppointmentStatusHistory { get; }
    DbSet<Consultation> Consultations { get; }
    DbSet<ConsultationSymptom> ConsultationSymptoms { get; }
    DbSet<VitalSigns> VitalSigns { get; }
    DbSet<ClinicalRecordRevision> ClinicalRecordRevisions { get; }
    DbSet<Medicine> Medicines { get; }
    DbSet<Prescription> Prescriptions { get; }
    DbSet<PrescriptionItem> PrescriptionItems { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<Payment> Payments { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Time source. Never use DateTime.Now/UtcNow directly.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    TimeZoneInfo HospitalTimeZone { get; }

    DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, HospitalTimeZone);
    DateOnly Today => DateOnly.FromDateTime(LocalNow);
    TimeOnly LocalTime => TimeOnly.FromDateTime(LocalNow);

    /// <summary>UTC instants bounding a hospital-local day: [start, end).</summary>
    (DateTime StartUtc, DateTime EndUtc) UtcRangeFor(DateOnly localDate)
    {
        var start = TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue), HospitalTimeZone);
        var end = TimeZoneInfo.ConvertTimeToUtc(localDate.AddDays(1).ToDateTime(TimeOnly.MinValue), HospitalTimeZone);
        return (start, end);
    }
}

public interface ICurrentUser
{
    long UserId { get; }
    Role Role { get; }
    long? DoctorId { get; }
    bool IsAuthenticated { get; }
}

/// <summary>Generates gap-free per-key sequence numbers. Must be called inside an open transaction.</summary>
public interface ISequenceGenerator
{
    Task<long> NextAsync(string key, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashRefreshToken(string token);
}

public sealed class HospitalOptions
{
    public const string Section = "Hospital";
    public string Name { get; set; } = "City Care Hospital";
    public string AddressLine { get; set; } = "";
    public string Phone { get; set; } = "";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string Currency { get; set; } = "INR";
    public int MaxDaysAhead { get; set; } = 90;
}
