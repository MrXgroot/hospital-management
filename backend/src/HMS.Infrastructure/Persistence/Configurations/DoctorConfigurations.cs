using HMS.Domain.Doctors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HMS.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_departments_name");
    }
}

public sealed class SpecializationConfiguration : IEntityTypeConfiguration<Specialization>
{
    public void Configure(EntityTypeBuilder<Specialization> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_specializations_name");
    }
}

public sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_doctors_fee", "`consultation_fee` >= 0"));
        b.Property(x => x.DoctorCode).HasMaxLength(10);
        b.Property(x => x.FirstName).HasMaxLength(80);
        b.Property(x => x.LastName).HasMaxLength(80);
        b.Property(x => x.Phone).HasMaxLength(16);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Qualification).HasMaxLength(200);
        b.Property(x => x.RegistrationNumber).HasMaxLength(50);
        b.Ignore(x => x.FullName);
        b.HasIndex(x => x.DoctorCode).IsUnique().HasDatabaseName("UX_doctors_doctor_code");
        b.HasIndex(x => x.RegistrationNumber).IsUnique().HasDatabaseName("UX_doctors_registration_number");
        b.HasIndex(x => new { x.DepartmentId, x.IsActive }).HasDatabaseName("IX_doctors_department_id_is_active");
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Specializations).WithOne().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Schedules).WithOne().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Leaves).WithOne().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class DoctorSpecializationConfiguration : IEntityTypeConfiguration<DoctorSpecialization>
{
    public void Configure(EntityTypeBuilder<DoctorSpecialization> b)
    {
        b.HasKey(x => new { x.DoctorId, x.SpecializationId });
        b.HasOne(x => x.Specialization).WithMany().HasForeignKey(x => x.SpecializationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DoctorScheduleConfiguration : IEntityTypeConfiguration<DoctorSchedule>
{
    public void Configure(EntityTypeBuilder<DoctorSchedule> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_doctor_schedules_time", "`end_time` > `start_time`");
            t.HasCheckConstraint("CK_doctor_schedules_slot", "`slot_duration_minutes` BETWEEN 5 AND 120");
        });
        b.HasIndex(x => new { x.DoctorId, x.DayOfWeek }).HasDatabaseName("IX_doctor_schedules_doctor_id_day");
    }
}

public sealed class DoctorLeaveConfiguration : IEntityTypeConfiguration<DoctorLeave>
{
    public void Configure(EntityTypeBuilder<DoctorLeave> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_doctor_leaves_dates", "`to_date` >= `from_date`"));
        b.Property(x => x.Reason).HasMaxLength(250);
        b.HasIndex(x => new { x.DoctorId, x.FromDate, x.ToDate }).HasDatabaseName("IX_doctor_leaves_doctor_id_dates");
    }
}
