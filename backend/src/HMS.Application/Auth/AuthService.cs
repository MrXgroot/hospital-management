using FluentValidation;
using HMS.Application.Common;
using HMS.Domain.Common;
using HMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Auth;

public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record UserDto(long Id, string Username, string FullName, string? Email, Role Role, long? DoctorId, bool IsActive, DateTime? LastLoginAt);
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);

public sealed record CreateUserRequest(string Username, string Password, string FullName, string? Email, Role Role, long? DoctorId);
public sealed record UpdateUserRequest(string FullName, string? Email, Role Role, long? DoctorId, bool IsActive, string? NewPassword);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(50).Matches("^[a-zA-Z0-9._-]+$");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.DoctorId).NotNull().When(x => x.Role == Role.Doctor).WithMessage("Doctor users must be linked to a doctor profile.");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(150).When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.NewPassword).MinimumLength(8).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.NewPassword));
        RuleFor(x => x.DoctorId).NotNull().When(x => x.Role == Role.Doctor).WithMessage("Doctor users must be linked to a doctor profile.");
    }
}

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct);
    Task LogoutAsync(RefreshRequest request, CancellationToken ct);
    Task<UserDto> GetCurrentAsync(CancellationToken ct);
}

public sealed class AuthService(
    IAppDbContext db, IPasswordHasher hasher, ITokenService tokens, IClock clock, ICurrentUser currentUser) : IAuthService
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);
        if (user is null || !user.IsActive || !hasher.Verify(request.Password, user.PasswordHash))
            throw new BusinessRuleException("INVALID_CREDENTIALS", "Invalid username or password.");

        user.LastLoginAt = clock.UtcNow;
        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var existing = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is null || !existing.IsActive(clock.UtcNow) || !existing.User.IsActive)
            throw new BusinessRuleException("INVALID_REFRESH_TOKEN", "Session expired. Please sign in again.");

        existing.RevokedAt = clock.UtcNow;
        var response = await IssueAsync(existing.User, ct);
        existing.ReplacedById = (await db.RefreshTokens.SingleAsync(t => t.TokenHash == tokens.HashRefreshToken(response.RefreshToken), ct)).Id;
        await db.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UserId == currentUser.UserId, ct);
        if (token is { RevokedAt: null })
        {
            token.RevokedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserDto> GetCurrentAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
                   ?? throw new NotFoundException("User", currentUser.UserId);
        return user.ToDto();
    }

    private async Task<AuthResponse> IssueAsync(User user, CancellationToken ct)
    {
        var (access, expiresAt) = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = tokens.HashRefreshToken(refresh),
            ExpiresAt = clock.UtcNow.Add(RefreshLifetime), CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return new AuthResponse(access, refresh, expiresAt, user.ToDto());
    }
}

public static class UserMappings
{
    public static UserDto ToDto(this User u) =>
        new(u.Id, u.Username, u.FullName, u.Email, u.Role, u.DoctorId, u.IsActive, u.LastLoginAt);
}
