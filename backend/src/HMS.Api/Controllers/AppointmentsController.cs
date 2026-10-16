using HMS.Api.Infrastructure;
using HMS.Application.Appointments;
using HMS.Application.Common;
using HMS.Domain.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HMS.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize(Roles = Roles.ClinicalAndReception)]
public sealed class AppointmentsController(IAppointmentService appointments, IClock clock) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AppointmentDto>> List(
        [FromQuery] DateOnly? date, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] long? doctorId,
        [FromQuery] long? patientId, [FromQuery] AppointmentStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        appointments.ListAsync(new AppointmentQuery(date, fromDate, toDate, doctorId, patientId, status), new PageQuery(page, pageSize), ct);

    [HttpGet("queue")]
    public Task<IReadOnlyList<AppointmentDto>> Queue([FromQuery] DateOnly? date, [FromQuery] long? doctorId, CancellationToken ct) =>
        appointments.GetQueueAsync(date ?? clock.Today, doctorId, ct);

    [HttpGet("{id:long}")]
    public Task<AppointmentDto> Get(long id, CancellationToken ct) => appointments.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.AdminReception)]
    public async Task<ActionResult<AppointmentDto>> Create(CreateAppointmentRequest request, CancellationToken ct)
    {
        var appointment = await appointments.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = appointment.Id }, appointment);
    }

    [HttpPut("{id:long}/reschedule"), Authorize(Roles = Roles.AdminReception)]
    public Task<AppointmentDto> Reschedule(long id, RescheduleAppointmentRequest request, CancellationToken ct) =>
        appointments.RescheduleAsync(id, request, ct);

    [HttpPost("{id:long}/confirm"), Authorize(Roles = Roles.AdminReception)]
    public Task<AppointmentDto> Confirm(long id, CancellationToken ct) => appointments.ConfirmAsync(id, ct);

    [HttpPost("{id:long}/check-in"), Authorize(Roles = Roles.FrontDesk)]
    public Task<AppointmentDto> CheckIn(long id, CancellationToken ct) => appointments.CheckInAsync(id, ct);

    [HttpPost("{id:long}/cancel"), Authorize(Roles = Roles.AdminReception)]
    public Task<AppointmentDto> Cancel(long id, CancelAppointmentRequest request, CancellationToken ct) =>
        appointments.CancelAsync(id, request, ct);

    [HttpPost("{id:long}/no-show"), Authorize(Roles = Roles.AdminReception)]
    public Task<AppointmentDto> NoShow(long id, CancellationToken ct) => appointments.MarkNoShowAsync(id, ct);
}
