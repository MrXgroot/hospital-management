using HMS.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HMS.Infrastructure.Persistence.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> b)
    {
        b.Property(x => x.InvoiceNumber).HasMaxLength(20);
        b.Property(x => x.CancelReason).HasMaxLength(300);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.Property(x => x.BalanceDue).HasComputedColumnSql("(`total_amount` - `paid_amount`)", stored: true);
        b.HasIndex(x => x.InvoiceNumber).IsUnique().HasDatabaseName("UX_invoices_invoice_number");
        b.HasIndex(x => x.AppointmentId).IsUnique().HasDatabaseName("UX_invoices_appointment_id");
        b.HasIndex(x => x.Status).HasDatabaseName("IX_invoices_status");
        b.HasIndex(x => x.PatientId).HasDatabaseName("IX_invoices_patient_id");
        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> b)
    {
        b.Property(x => x.Description).HasMaxLength(200);
        b.HasIndex(x => x.InvoiceId).HasDatabaseName("IX_invoice_items_invoice_id");
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable(t => t.HasCheckConstraint("CK_payments_amount", "`amount` > 0"));
        b.Property(x => x.ReferenceNo).HasMaxLength(60);
        b.HasIndex(x => x.InvoiceId).HasDatabaseName("IX_payments_invoice_id");
        b.HasIndex(x => x.PaidAt).HasDatabaseName("IX_payments_paid_at");
    }
}
