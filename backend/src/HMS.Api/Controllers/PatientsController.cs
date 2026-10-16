using HMS.Api.Infrastructure;
using HMS.Application.Appointments;
using HMS.Application.Clinical;
using HMS.Application.Common;
using HMS.Application.Patients;
using HMS.Application.Prescriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HMS.Api.Controllers;

[ApiController]
[Route("api/patients")]
[Authorize(Roles = Roles.ClinicalAndReception)]
public sealed class PatientsController(
    IPatientService patients, IAppointmentService appointments, IConsultationService consultations,
    IPrescriptionService prescriptions, IClock clock) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PatientListItemDto>> Search([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        patients.SearchAsync(search, new PageQuery(page, Math.Min(pageSize, 50)), ct);

    [HttpGet("{id:long}")]
    public Task<PatientDto> Get(long id, CancellationToken ct) => patients.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.AdminReception)]
    public async Task<ActionResult<PatientDto>> Create(SavePatientRequest request, [FromQuery] bool confirmDuplicate, CancellationToken ct)
    {
        var patient = await patients.CreateAsync(request, confirmDuplicate, ct);
        return CreatedAtAction(nameof(Get), new { id = patient.Id }, patient);
    }

    [HttpPut("{id:long}"), Authorize(Roles = Roles.AdminReception)]
    public Task<PatientDto> Update(long id, SavePatientRequest request, CancellationToken ct) => patients.UpdateAsync(id, request, ct);

    [HttpDelete("{id:long}"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await patients.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:long}/visits")]
    public Task<PagedResult<VisitDto>> Visits(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        patients.GetVisitsAsync(id, new PageQuery(page, pageSize), ct);

    [HttpGet("{id:long}/appointments")]
    public Task<PagedResult<AppointmentDto>> Appointments(long id, [FromQuery] bool upcoming, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        appointments.ListAsync(new AppointmentQuery(null, upcoming ? clock.Today : null, null, null, id, null), new PageQuery(page, pageSize), ct);

    [HttpGet("{id:long}/vitals"), Authorize(Roles = Roles.Clinical)]
    public Task<IReadOnlyList<VitalSignsDto>> Vitals(long id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        consultations.GetPatientVitalsAsync(id, from, to, ct);

    [HttpGet("{id:long}/prescriptions")]
    public Task<IReadOnlyList<PrescriptionListItemDto>> Prescriptions(long id, CancellationToken ct) =>
        prescriptions.ListForPatientAsync(id, ct);

    [HttpPost("{id:long}/allergies")]
    public Task<AllergyDto> AddAllergy(long id, SaveAllergyRequest request, CancellationToken ct) =>
        patients.SaveAllergyAsync(id, null, request, ct);

    [HttpPut("{id:long}/allergies/{allergyId:long}")]
    public Task<AllergyDto> UpdateAllergy(long id, long allergyId, SaveAllergyRequest request, CancellationToken ct) =>
        patients.SaveAllergyAsync(id, allergyId, request, ct);

    [HttpDelete("{id:long}/allergies/{allergyId:long}")]
    public async Task<IActionResult> DeleteAllergy(long id, long allergyId, CancellationToken ct)
    {
        await patients.DeleteAllergyAsync(id, allergyId, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/conditions"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<ConditionDto> AddCondition(long id, SaveConditionRequest request, CancellationToken ct) =>
        patients.SaveConditionAsync(id, null, request, ct);

    [HttpPut("{id:long}/conditions/{conditionId:long}"), Authorize(Roles = Roles.AdminDoctor)]
    public Task<ConditionDto> UpdateCondition(long id, long conditionId, SaveConditionRequest request, CancellationToken ct) =>
        patients.SaveConditionAsync(id, conditionId, request, ct);

    [HttpDelete("{id:long}/conditions/{conditionId:long}"), Authorize(Roles = Roles.AdminDoctor)]
    public async Task<IActionResult> DeleteCondition(long id, long conditionId, CancellationToken ct)
    {
        await patients.DeleteConditionAsync(id, conditionId, ct);
        return NoContent();
    }
}
