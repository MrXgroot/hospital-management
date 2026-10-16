using FluentValidation;
using HMS.Application.Common;
using HMS.Domain.Common;
using HMS.Domain.Doctors;
using HMS.Domain.Prescriptions;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.MasterData;

public sealed record DepartmentDto(long Id, string Name, string? Description, bool IsActive);
public sealed record SaveDepartmentRequest(string Name, string? Description, bool IsActive = true);
public sealed record SpecializationDto(long Id, string Name, bool IsActive);
public sealed record SaveSpecializationRequest(string Name, bool IsActive = true);
public sealed record MedicineDto(long Id, string Name, string? GenericName, string? Form, string? Strength, bool IsActive);
public sealed record SaveMedicineRequest(string Name, string? GenericName, string? Form, string? Strength, bool IsActive = true);

public sealed class SaveDepartmentRequestValidator : AbstractValidator<SaveDepartmentRequest>
{
    public SaveDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class SaveSpecializationRequestValidator : AbstractValidator<SaveSpecializationRequest>
{
    public SaveSpecializationRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
}

public sealed class SaveMedicineRequestValidator : AbstractValidator<SaveMedicineRequest>
{
    public SaveMedicineRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.GenericName).MaximumLength(150);
        RuleFor(x => x.Form).MaximumLength(30);
        RuleFor(x => x.Strength).MaximumLength(50);
    }
}

public interface IMasterDataService
{
    Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(bool activeOnly, CancellationToken ct);
    Task<DepartmentDto> SaveDepartmentAsync(long? id, SaveDepartmentRequest request, CancellationToken ct);
    Task<IReadOnlyList<SpecializationDto>> ListSpecializationsAsync(bool activeOnly, CancellationToken ct);
    Task<SpecializationDto> SaveSpecializationAsync(long? id, SaveSpecializationRequest request, CancellationToken ct);
    Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string? search, bool activeOnly, CancellationToken ct);
    Task<MedicineDto> SaveMedicineAsync(long? id, SaveMedicineRequest request, CancellationToken ct);
}

public sealed class MasterDataService(IAppDbContext db) : IMasterDataService
{
    public async Task<IReadOnlyList<DepartmentDto>> ListDepartmentsAsync(bool activeOnly, CancellationToken ct) =>
        await db.Departments.AsNoTracking().Where(d => !activeOnly || d.IsActive).OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Description, d.IsActive)).ToListAsync(ct);

    public async Task<DepartmentDto> SaveDepartmentAsync(long? id, SaveDepartmentRequest request, CancellationToken ct)
    {
        if (await db.Departments.AnyAsync(d => d.Name == request.Name && d.Id != id, ct))
            throw new ConflictException("DUPLICATE_NAME", $"Department '{request.Name}' already exists.");
        var entity = id is null ? db.Departments.Add(new Department()).Entity
            : await db.Departments.FindAsync([id.Value], ct) ?? throw new NotFoundException("Department", id);
        entity.Name = request.Name.Trim();
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new DepartmentDto(entity.Id, entity.Name, entity.Description, entity.IsActive);
    }

    public async Task<IReadOnlyList<SpecializationDto>> ListSpecializationsAsync(bool activeOnly, CancellationToken ct) =>
        await db.Specializations.AsNoTracking().Where(s => !activeOnly || s.IsActive).OrderBy(s => s.Name)
            .Select(s => new SpecializationDto(s.Id, s.Name, s.IsActive)).ToListAsync(ct);

    public async Task<SpecializationDto> SaveSpecializationAsync(long? id, SaveSpecializationRequest request, CancellationToken ct)
    {
        if (await db.Specializations.AnyAsync(s => s.Name == request.Name && s.Id != id, ct))
            throw new ConflictException("DUPLICATE_NAME", $"Specialization '{request.Name}' already exists.");
        var entity = id is null ? db.Specializations.Add(new Specialization()).Entity
            : await db.Specializations.FindAsync([id.Value], ct) ?? throw new NotFoundException("Specialization", id);
        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new SpecializationDto(entity.Id, entity.Name, entity.IsActive);
    }

    public async Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string? search, bool activeOnly, CancellationToken ct)
    {
        var query = db.Medicines.AsNoTracking().Where(m => !activeOnly || m.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(m => m.Name.StartsWith(s) || (m.GenericName != null && m.GenericName.StartsWith(s)));
        }
        return await query.OrderBy(m => m.Name).Take(50)
            .Select(m => new MedicineDto(m.Id, m.Name, m.GenericName, m.Form, m.Strength, m.IsActive)).ToListAsync(ct);
    }

    public async Task<MedicineDto> SaveMedicineAsync(long? id, SaveMedicineRequest request, CancellationToken ct)
    {
        var entity = id is null ? db.Medicines.Add(new Medicine()).Entity
            : await db.Medicines.FindAsync([id.Value], ct) ?? throw new NotFoundException("Medicine", id);
        entity.Name = request.Name.Trim();
        entity.GenericName = request.GenericName;
        entity.Form = request.Form;
        entity.Strength = request.Strength;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new MedicineDto(entity.Id, entity.Name, entity.GenericName, entity.Form, entity.Strength, entity.IsActive);
    }
}
