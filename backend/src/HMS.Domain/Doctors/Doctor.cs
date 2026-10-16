using HMS.Domain.Common;

namespace HMS.Domain.Doctors;

public class Department : AuditableEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Specialization : AuditableEntity
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Doctor : AuditableEntity, ISoftDelete, IAudited
{
    public string DoctorCode { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public Gender Gender { get; set; }
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string Qualification { get; set; } = "";
    public string RegistrationNumber { get; set; } = "";
    public long DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public decimal ConsultationFee { get; set; }
    public short? YearsOfExperience { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }

    public List<DoctorSpecialization> Specializations { get; set; } = [];
    public List<DoctorSchedule> Schedules { get; set; } = [];
    public List<DoctorLeave> Leaves { get; set; } = [];

    public string FullName => $"Dr. {FirstName} {LastName}";
}

public class DoctorSpecialization
{
    public long DoctorId { get; set; }
    public long SpecializationId { get; set; }
    public Specialization Specialization { get; set; } = null!;
    public bool IsPrimary { get; set; }
}

public class DoctorSchedule : AuditableEntity
{
    public long DoctorId { get; set; }
    /// <summary>0 = Sunday .. 6 = Saturday (System.DayOfWeek).</summary>
    public byte DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public short SlotDurationMinutes { get; set; } = 15;
    public bool IsActive { get; set; } = true;
}

public class DoctorLeave : AuditableEntity
{
    public long DoctorId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string? Reason { get; set; }

    public bool Covers(DateOnly date) => date >= FromDate && date <= ToDate;
}
