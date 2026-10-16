using HMS.Api.Infrastructure;
using HMS.Application.Billing;
using HMS.Application.Clinical;
using HMS.Application.Common;
using HMS.Application.Prescriptions;
using HMS.Domain.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HMS.Api.Controllers;

[ApiController]
[Route("api/consultations")]
[Authorize(Roles = Roles.Clinical)]
public sealed class ConsultationsController(IConsultationService consultations, IPrescriptionService prescriptions) : ControllerBase
{
    [HttpPost, Authorize(Roles = Roles.DoctorOnly)]
    public Task<ConsultationDto> Start(StartConsultationRequest request, CancellationToken ct) => consultations.StartAsync(request, ct);

    [HttpGet("{id:long}")]
    public Task<ConsultationDto> Get(long id, CancellationToken ct) => consultations.GetAsync(id, ct);

    [HttpPut("{id:long}"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<ConsultationDto> Update(long id, UpdateConsultationRequest request, CancellationToken ct) =>
        consultations.UpdateAsync(id, request, ct);

    [HttpPost("{id:long}/finalise"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<ConsultationDto> Finalise(long id, CancellationToken ct) => consultations.FinaliseAsync(id, ct);

    [HttpGet("{id:long}/history"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<IReadOnlyList<RevisionDto>> History(long id, CancellationToken ct) => consultations.GetHistoryAsync(id, ct);

    [HttpPost("{id:long}/vitals")]
    public Task<VitalSignsDto> AddVitals(long id, SaveVitalsRequest request, CancellationToken ct) =>
        consultations.AddVitalsAsync(id, request, ct);

    [HttpPut("{id:long}/vitals/{vitalsId:long}")]
    public Task<VitalSignsDto> UpdateVitals(long id, long vitalsId, SaveVitalsRequest request, CancellationToken ct) =>
        consultations.UpdateVitalsAsync(id, vitalsId, request, ct);

    [HttpPost("{id:long}/prescription"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<PrescriptionDto> CreatePrescription(long id, SavePrescriptionRequest request, CancellationToken ct) =>
        prescriptions.CreateAsync(id, request, ct);
}

[ApiController]
[Route("api/prescriptions")]
[Authorize(Roles = Roles.ClinicalAndReception)]
public sealed class PrescriptionsController(IPrescriptionService prescriptions) : ControllerBase
{
    [HttpGet("{id:long}")]
    public Task<PrescriptionDto> Get(long id, CancellationToken ct) => prescriptions.GetAsync(id, ct);

    [HttpPut("{id:long}"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<PrescriptionDto> Update(long id, SavePrescriptionRequest request, CancellationToken ct) =>
        prescriptions.UpdateAsync(id, request, ct);

    [HttpPost("{id:long}/finalise"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<PrescriptionDto> Finalise(long id, CancellationToken ct) => prescriptions.FinaliseAsync(id, ct);

    [HttpGet("{id:long}/print")]
    public Task<PrescriptionPrintDto> Print(long id, CancellationToken ct) => prescriptions.GetPrintAsync(id, ct);

    [HttpGet("{id:long}/history"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<IReadOnlyList<RevisionDto>> History(long id, CancellationToken ct) => prescriptions.GetHistoryAsync(id, ct);
}

[ApiController]
[Route("api/invoices")]
[Authorize(Roles = Roles.AdminReception)]
public sealed class InvoicesController(IBillingService billing) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<InvoiceListItemDto>> List([FromQuery] InvoiceStatus? status, [FromQuery] DateOnly? date,
        [FromQuery] long? patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        billing.ListAsync(new InvoiceQuery(status, date, patientId), new PageQuery(page, pageSize), ct);

    [HttpGet("{id:long}")]
    public Task<InvoiceDto> Get(long id, CancellationToken ct) => billing.GetAsync(id, ct);

    [HttpPost("{id:long}/mark-paid")]
    public Task<InvoiceDto> MarkPaid(long id, MarkPaidRequest request, CancellationToken ct) => billing.MarkPaidAsync(id, request, ct);

    [HttpPost("{id:long}/cancel"), Authorize(Roles = Roles.Admin)]
    public Task<InvoiceDto> Cancel(long id, CancelInvoiceRequest request, CancellationToken ct) => billing.CancelAsync(id, request, ct);
}
