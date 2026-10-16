using HMS.Domain.Common;
using HMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HMS.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.Username).HasMaxLength(50);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.PasswordHash).HasMaxLength(100);
        b.Property(x => x.FullName).HasMaxLength(150);
        b.HasIndex(x => x.Username).IsUnique().HasDatabaseName("UX_users_username");
        b.HasIndex(x => x.DoctorId).IsUnique().HasDatabaseName("UX_users_doctor_id");
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(x => x.TokenHash).HasMaxLength(64).IsFixedLength();
        b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UX_refresh_tokens_token_hash");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class IdSequenceConfiguration : IEntityTypeConfiguration<IdSequence>
{
    public void Configure(EntityTypeBuilder<IdSequence> b)
    {
        b.ToTable("id_sequences");
        b.HasKey(x => x.SeqKey);
        b.Property(x => x.SeqKey).HasMaxLength(40);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.EntityType).HasMaxLength(50);
        b.Property(x => x.Action).HasMaxLength(20);
        b.Property(x => x.ChangesJson).HasColumnType("json");
        b.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("IX_audit_logs_entity");
        b.HasIndex(x => x.ChangedAt).HasDatabaseName("IX_audit_logs_changed_at");
    }
}
