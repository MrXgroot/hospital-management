using HMS.Domain.Common;
using HMS.Domain.Doctors;

namespace HMS.Domain.Identity;

public enum Role { Admin, Receptionist, Doctor, Nurse }

public class User : AuditableEntity, IAudited
{
    public string Username { get; set; } = "";
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public Role Role { get; set; }
    public long? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}

public class RefreshToken : Entity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public long? ReplacedById { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
