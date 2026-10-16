using HMS.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HMS.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_appointments_time", "`end_time` > `start_time`"));
        b.Property(x => x.ReasonForVisit).HasMaxLength(300);
        b.Property(x => x.CancellationReason).HasMaxLength(300);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.Ignore(x => x.HoldsSlot);

        // NULL once the appointment stops holding its slot, so the unique index below ignores it
        // (MySQL allows any number of NULLs in a unique index). Keep in sync with Appointment.SlotReleasingStatuses.
        b.Property(x => x.ActiveSlot)
            .HasColumnType("tinyint")
            .HasComputedColumnSql("(case when `status` in ('Cancelled','NoShow') then NULL else 1 end)", stored: true);

        b.HasIndex(x => new { x.DoctorId, x.AppointmentDate, x.StartTime, x.ActiveSlot })
            .IsUnique().HasDatabaseName("UX_appointments_active_slot");
        b.HasIndex(x => new { x.DoctorId, x.AppointmentDate, x.QueueToken })
            .IsUnique().HasDatabaseName("UX_appointments_queue_token");
        b.HasIndex(x => new { x.PatientId, x.AppointmentDate }).HasDatabaseName("IX_appointments_patient_id_date");
        b.HasIndex(x => new { x.AppointmentDate, x.Status }).HasDatabaseName("IX_appointments_date_status");

        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AppointmentStatusHistoryConfiguration : IEntityTypeConfiguration<AppointmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentStatusHistory> b)
    {
        b.Property(x => x.Note).HasMaxLength(300);
        b.HasIndex(x => x.AppointmentId).HasDatabaseName("IX_appointment_status_history_appointment_id");
    }
}
