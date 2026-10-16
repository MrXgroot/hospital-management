using HMS.Application.Common;
using HMS.Domain.Common;
using HMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Auth;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);
    Task<UserDto> GetAsync(long id, CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct);
}

public sealed class UserService(IAppDbContext db, IPasswordHasher hasher, ICurrentUser currentUser) : IUserService
{
    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct) =>
        (await db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync(ct)).Select(u => u.ToDto()).ToList();

    public async Task<UserDto> GetAsync(long id, CancellationToken ct) =>
        (await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User", id)).ToDto();

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Username == request.Username, ct))
            throw new ConflictException("USERNAME_TAKEN", $"Username '{request.Username}' is already in use.");
        var doctorId = await ValidateDoctorLinkAsync(request.Role, request.DoctorId, null, ct);

        var user = new User
        {
            Username = request.Username.Trim(), FullName = request.FullName.Trim(), Email = request.Email,
            Role = request.Role, DoctorId = doctorId, PasswordHash = hasher.Hash(request.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User", id);
        if (user.Id == currentUser.UserId && (!request.IsActive || request.Role != Role.Admin) && user.Role == Role.Admin)
            throw new BusinessRuleException("CANNOT_DEMOTE_SELF", "You cannot deactivate or demote your own admin account.");

        user.DoctorId = await ValidateDoctorLinkAsync(request.Role, request.DoctorId, id, ct);
        user.FullName = request.FullName.Trim();
        user.Email = request.Email;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrEmpty(request.NewPassword))
            user.PasswordHash = hasher.Hash(request.NewPassword);
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    private async Task<long?> ValidateDoctorLinkAsync(Role role, long? doctorId, long? userId, CancellationToken ct)
    {
        if (role != Role.Doctor) return null;
        if (!await db.Doctors.AnyAsync(d => d.Id == doctorId, ct))
            throw new NotFoundException("Doctor", doctorId!);
        if (await db.Users.AnyAsync(u => u.DoctorId == doctorId && u.Id != userId, ct))
            throw new ConflictException("DOCTOR_ALREADY_LINKED", "Another user is already linked to this doctor.");
        return doctorId;
    }
}
