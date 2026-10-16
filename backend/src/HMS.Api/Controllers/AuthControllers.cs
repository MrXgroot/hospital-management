using HMS.Api.Infrastructure;
using HMS.Application.Auth;
using HMS.Application.Dashboard;
using HMS.Application.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("login")]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct) => auth.RefreshAsync(request, ct);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    [HttpGet("me")]
    public Task<UserDto> Me(CancellationToken ct) => auth.GetCurrentAsync(ct);
}

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public sealed class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> List(CancellationToken ct) => users.ListAsync(ct);

    [HttpGet("{id:long}")]
    public Task<UserDto> Get(long id, CancellationToken ct) => users.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpPut("{id:long}")]
    public Task<UserDto> Update(long id, UpdateUserRequest request, CancellationToken ct) => users.UpdateAsync(id, request, ct);
}

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.ClinicalAndReception)]
public sealed class DashboardController(IDashboardService dashboard) : ControllerBase
{
    [HttpGet("summary")]
    public Task<DashboardSummaryDto> Summary(CancellationToken ct) => dashboard.GetSummaryAsync(ct);
}

[ApiController]
[Route("api")]
public sealed class MasterDataController(IMasterDataService master) : ControllerBase
{
    [HttpGet("departments")]
    public Task<IReadOnlyList<DepartmentDto>> Departments([FromQuery] bool activeOnly, CancellationToken ct) =>
        master.ListDepartmentsAsync(activeOnly, ct);

    [HttpPost("departments"), Authorize(Roles = Roles.Admin)]
    public Task<DepartmentDto> CreateDepartment(SaveDepartmentRequest request, CancellationToken ct) =>
        master.SaveDepartmentAsync(null, request, ct);

    [HttpPut("departments/{id:long}"), Authorize(Roles = Roles.Admin)]
    public Task<DepartmentDto> UpdateDepartment(long id, SaveDepartmentRequest request, CancellationToken ct) =>
        master.SaveDepartmentAsync(id, request, ct);

    [HttpGet("specializations")]
    public Task<IReadOnlyList<SpecializationDto>> Specializations([FromQuery] bool activeOnly, CancellationToken ct) =>
        master.ListSpecializationsAsync(activeOnly, ct);

    [HttpPost("specializations"), Authorize(Roles = Roles.Admin)]
    public Task<SpecializationDto> CreateSpecialization(SaveSpecializationRequest request, CancellationToken ct) =>
        master.SaveSpecializationAsync(null, request, ct);

    [HttpPut("specializations/{id:long}"), Authorize(Roles = Roles.Admin)]
    public Task<SpecializationDto> UpdateSpecialization(long id, SaveSpecializationRequest request, CancellationToken ct) =>
        master.SaveSpecializationAsync(id, request, ct);

    [HttpGet("medicines")]
    public Task<IReadOnlyList<MedicineDto>> Medicines([FromQuery] string? search, [FromQuery] bool activeOnly, CancellationToken ct) =>
        master.SearchMedicinesAsync(search, activeOnly, ct);

    [HttpPost("medicines"), Authorize(Roles = Roles.Admin)]
    public Task<MedicineDto> CreateMedicine(SaveMedicineRequest request, CancellationToken ct) =>
        master.SaveMedicineAsync(null, request, ct);

    [HttpPut("medicines/{id:long}"), Authorize(Roles = Roles.Admin)]
    public Task<MedicineDto> UpdateMedicine(long id, SaveMedicineRequest request, CancellationToken ct) =>
        master.SaveMedicineAsync(id, request, ct);
}
