using HMS.Domain.Appointments;
using HMS.Domain.Common;
using HMS.Domain.Patients;

namespace HMS.Domain.Billing;

public enum InvoiceStatus { Unpaid, Paid, Cancelled }

public enum PaymentMethod { Cash, Card, UPI, Other }

public enum InvoiceItemType { ConsultationFee, Procedure, Other }

public class Invoice : AuditableEntity, IVersioned, IAudited
{
    public string InvoiceNumber { get; set; } = "";
    public long PatientId { get; set; }
    public Patient Patient { get; set; } = null!;
    public long? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    /// <summary>DB-generated: total_amount - paid_amount.</summary>
    public decimal BalanceDue { get; private set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Unpaid;
    public string? CancelReason { get; set; }
    public int RowVersion { get; set; }

    public List<InvoiceItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    public void AddItem(string description, InvoiceItemType type, short quantity, decimal unitPrice)
    {
        Items.Add(new InvoiceItem
        {
            Description = description, ItemType = type, Quantity = quantity,
            UnitPrice = unitPrice, LineTotal = quantity * unitPrice,
        });
        Subtotal = Items.Sum(i => i.LineTotal);
        TotalAmount = Subtotal - DiscountAmount;
    }

    /// <summary>Demo payment flow: the receptionist marks the full outstanding amount as received.</summary>
    public Payment MarkPaid(PaymentMethod method, string? referenceNo, DateTime utcNow, long userId)
    {
        if (Status != InvoiceStatus.Unpaid)
            throw new BusinessRuleException("INVOICE_NOT_UNPAID", $"Only unpaid invoices can be marked paid (current: {Status}).");

        var payment = new Payment
        {
            Amount = TotalAmount - PaidAmount, Method = method, ReferenceNo = referenceNo,
            PaidAt = utcNow, ReceivedBy = userId,
        };
        Payments.Add(payment);
        PaidAmount = TotalAmount;
        Status = InvoiceStatus.Paid;
        return payment;
    }

    public void Cancel(string reason)
    {
        if (Status == InvoiceStatus.Paid)
            throw new BusinessRuleException("INVOICE_ALREADY_PAID", "A paid invoice cannot be cancelled.");
        if (Status == InvoiceStatus.Cancelled) return;
        Status = InvoiceStatus.Cancelled;
        CancelReason = reason;
    }
}

public class InvoiceItem : Entity
{
    public long InvoiceId { get; set; }
    public string Description { get; set; } = "";
    public InvoiceItemType ItemType { get; set; }
    public short Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class Payment : Entity
{
    public long InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? ReferenceNo { get; set; }
    public DateTime PaidAt { get; set; }
    public long ReceivedBy { get; set; }
}
