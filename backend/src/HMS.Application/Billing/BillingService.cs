using FluentValidation;
using HMS.Application.Common;
using HMS.Domain.Appointments;
using HMS.Domain.Billing;
using HMS.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Billing;

public sealed record InvoiceItemDto(long Id, string Description, InvoiceItemType ItemType, short Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record PaymentDto(long Id, decimal Amount, PaymentMethod Method, string? ReferenceNo, DateTime PaidAt, string? ReceivedByName);

public sealed record InvoiceListItemDto(
    long Id, string InvoiceNumber, long PatientId, string PatientName, string PatientMrn, string? DoctorName,
    DateOnly InvoiceDate, decimal TotalAmount, decimal BalanceDue, InvoiceStatus Status);

public sealed record InvoiceDto(
    long Id, string InvoiceNumber, long PatientId, string PatientName, string PatientMrn, long? AppointmentId,
    string? DoctorName, DateOnly InvoiceDate, decimal Subtotal, decimal DiscountAmount, decimal TotalAmount,
    decimal PaidAmount, decimal BalanceDue, InvoiceStatus Status, string? CancelReason,
    IReadOnlyList<InvoiceItemDto> Items, IReadOnlyList<PaymentDto> Payments, int RowVersion);

public sealed record InvoiceQuery(InvoiceStatus? Status, DateOnly? Date, long? PatientId);
public sealed record MarkPaidRequest(PaymentMethod Method, string? ReferenceNo);
public sealed record CancelInvoiceRequest(string Reason);

public sealed class MarkPaidRequestValidator : AbstractValidator<MarkPaidRequest>
{
    public MarkPaidRequestValidator()
    {
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.ReferenceNo).MaximumLength(60);
    }
}

public sealed class CancelInvoiceRequestValidator : AbstractValidator<CancelInvoiceRequest>
{
    public CancelInvoiceRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
}

public interface IBillingService
{
    /// <summary>Adds an invoice for the doctor's current fee. Caller must hold an open transaction and save.</summary>
    Task<Invoice> CreateConsultationInvoiceAsync(Appointment appointment, CancellationToken ct);
    Task<PagedResult<InvoiceListItemDto>> ListAsync(InvoiceQuery query, PageQuery page, CancellationToken ct);
    Task<InvoiceDto> GetAsync(long id, CancellationToken ct);
    Task<InvoiceDto> MarkPaidAsync(long id, MarkPaidRequest request, CancellationToken ct);
    Task<InvoiceDto> CancelAsync(long id, CancelInvoiceRequest request, CancellationToken ct);
}

public sealed class BillingService(IAppDbContext db, IClock clock, ICurrentUser currentUser, ISequenceGenerator sequences)
    : IBillingService
{
    public async Task<Invoice> CreateConsultationInvoiceAsync(Appointment appointment, CancellationToken ct)
    {
        var doctor = await db.Doctors.AsNoTracking().Where(d => d.Id == appointment.DoctorId)
            .Select(d => new { d.FirstName, d.LastName, d.ConsultationFee }).SingleAsync(ct);
        var today = clock.Today;
        var invoice = new Invoice
        {
            InvoiceNumber = IdFormats.InvoiceNumber(today, await sequences.NextAsync(IdFormats.InvoiceSequenceKey(today), ct)),
            PatientId = appointment.PatientId,
            Appointment = appointment,
            InvoiceDate = today,
        };
        invoice.AddItem($"Consultation fee - Dr. {doctor.FirstName} {doctor.LastName}", InvoiceItemType.ConsultationFee, 1, doctor.ConsultationFee);
        db.Invoices.Add(invoice);
        return invoice;
    }

    public async Task<PagedResult<InvoiceListItemDto>> ListAsync(InvoiceQuery query, PageQuery page, CancellationToken ct)
    {
        var q = db.Invoices.AsNoTracking();
        if (query.Status is { } s) q = q.Where(i => i.Status == s);
        if (query.Date is { } d) q = q.Where(i => i.InvoiceDate == d);
        if (query.PatientId is { } p) q = q.Where(i => i.PatientId == p);
        return await q.OrderByDescending(i => i.Id)
            .Select(i => new InvoiceListItemDto(i.Id, i.InvoiceNumber, i.PatientId,
                i.Patient.FirstName + " " + i.Patient.LastName, i.Patient.Mrn,
                i.Appointment != null ? "Dr. " + i.Appointment.Doctor.FirstName + " " + i.Appointment.Doctor.LastName : null,
                i.InvoiceDate, i.TotalAmount, i.BalanceDue, i.Status))
            .ToPagedAsync(page, ct);
    }

    public async Task<InvoiceDto> GetAsync(long id, CancellationToken ct)
    {
        var i = await db.Invoices.AsNoTracking()
                    .Include(x => x.Patient).Include(x => x.Items).Include(x => x.Payments)
                    .Include(x => x.Appointment).ThenInclude(a => a!.Doctor)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Invoice", id);
        var receiverIds = i.Payments.Select(p => p.ReceivedBy).Distinct().ToList();
        var receivers = await db.Users.AsNoTracking().Where(u => receiverIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        return new InvoiceDto(i.Id, i.InvoiceNumber, i.PatientId, i.Patient.FullName, i.Patient.Mrn, i.AppointmentId,
            i.Appointment?.Doctor.FullName, i.InvoiceDate, i.Subtotal, i.DiscountAmount, i.TotalAmount, i.PaidAmount,
            i.BalanceDue, i.Status, i.CancelReason,
            i.Items.Select(x => new InvoiceItemDto(x.Id, x.Description, x.ItemType, x.Quantity, x.UnitPrice, x.LineTotal)).ToList(),
            i.Payments.OrderBy(p => p.PaidAt).Select(p => new PaymentDto(p.Id, p.Amount, p.Method, p.ReferenceNo, p.PaidAt,
                receivers.GetValueOrDefault(p.ReceivedBy))).ToList(),
            i.RowVersion);
    }

    public async Task<InvoiceDto> MarkPaidAsync(long id, MarkPaidRequest request, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id, ct)
                      ?? throw new NotFoundException("Invoice", id);
        invoice.MarkPaid(request.Method, request.ReferenceNo, clock.UtcNow, currentUser.UserId);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<InvoiceDto> CancelAsync(long id, CancelInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Invoice", id);
        invoice.Cancel(request.Reason.Trim());
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }
}
