using HMS.Api.Infrastructure;
using HMS.Application.Doctors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HMS.Api.Controllers;

[ApiController]
[Route("api/doctors")]
public sealed class DoctorsController(IDoctorService doctors) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DoctorListItemDto>> List(
        [FromQuery] string? search, [FromQuery] long? departmentId, [FromQuery] long? specializationId,
        [FromQuery] bool? isActive, CancellationToken ct) =>
        doctors.ListAsync(new DoctorQuery(search, departmentId, specializationId, isActive), ct);

    [HttpGet("available-today")]
    public Task<IReadOnlyList<AvailableDoctorDto>> AvailableToday(CancellationToken ct) => doctors.GetAvailableTodayAsync(ct);

    [HttpGet("{id:long}")]
    public Task<DoctorDto> Get(long id, CancellationToken ct) => doctors.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<DoctorDto>> Create(SaveDoctorRequest request, CancellationToken ct)
    {
        var doctor = await doctors.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = doctor.Id }, doctor);
    }

    [HttpPut("{id:long}"), Authorize(Roles = Roles.Admin)]
    public Task<DoctorDto> Update(long id, SaveDoctorRequest request, CancellationToken ct) => doctors.UpdateAsync(id, request, ct);

    [HttpPatch("{id:long}/status"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> SetStatus(long id, SetDoctorStatusRequest request, CancellationToken ct)
    {
        await doctors.SetStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/schedule")]
    public Task<IReadOnlyList<ScheduleBlockDto>> GetSchedule(long id, CancellationToken ct) => doctors.GetScheduleAsync(id, ct);

    [HttpPut("{id:long}/schedule"), Authorize(Roles = Roles.Admin)]
    public Task<IReadOnlyList<ScheduleBlockDto>> SaveSchedule(long id, SaveScheduleRequest request, CancellationToken ct) =>
        doctors.SaveScheduleAsync(id, request, ct);

    [HttpGet("{id:long}/leaves")]
    public Task<IReadOnlyList<DoctorLeaveDto>> Leaves(long id, CancellationToken ct) => doctors.ListLeavesAsync(id, ct);

    [HttpPost("{id:long}/leaves"), Authorize(Roles = Roles.Admin)]
    public Task<DoctorLeaveDto> AddLeave(long id, CreateLeaveRequest request, CancellationToken ct) => doctors.AddLeaveAsync(id, request, ct);

    [HttpDelete("{id:long}/leaves/{leaveId:long}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeleteLeave(long id, long leaveId, CancellationToken ct)
    {
        await doctors.DeleteLeaveAsync(id, leaveId, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/slots"), Authorize(Roles = Roles.ClinicalAndReception)]
    public Task<IReadOnlyList<SlotDto>> Slots(long id, [FromQuery] DateOnly date, CancellationToken ct) =>
        doctors.GetSlotsAsync(id, date, ct);
}
