using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HMS.Application.Common;
using HMS.Domain.Identity;
using HMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HMS.Infrastructure.Services;

public sealed class SystemClock(TimeProvider timeProvider, IOptions<HospitalOptions> options) : IClock
{
    public DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    public TimeZoneInfo HospitalTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
}

/// <summary>
/// Row-locked counter per key. The upsert takes an exclusive lock on the row that is held until the
/// surrounding transaction commits, so concurrent callers are serialised and never get the same value.
/// </summary>
public sealed class SequenceGenerator(HmsDbContext db) : ISequenceGenerator
{
    public async Task<long> NextAsync(string key, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Sequence numbers must be generated inside a transaction.");

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO id_sequences (seq_key, current_value) VALUES ({key}, 1) ON DUPLICATE KEY UPDATE current_value = current_value + 1", ct);
        return await db.Database
            .SqlQuery<long>($"SELECT current_value AS `Value` FROM id_sequences WHERE seq_key = {key}")
            .SingleAsync(ct);
    }
}

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "hms-api";
    public string Audience { get; set; } = "hms-web";
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
}

public static class HmsClaims
{
    public const string UserId = "sub";
    public const string Username = "name";
    public const string Role = "role";
    public const string FullName = "full_name";
    public const string DoctorId = "doctor_id";
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var o = options.Value;
        var claims = new List<Claim>
        {
            new(HmsClaims.UserId, user.Id.ToString()),
            new(HmsClaims.Username, user.Username),
            new(HmsClaims.Role, user.Role.ToString()),
            new(HmsClaims.FullName, user.FullName),
        };
        if (user.DoctorId is { } doctorId) claims.Add(new Claim(HmsClaims.DoctorId, doctorId.ToString()));

        var expires = clock.UtcNow.AddMinutes(o.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey));
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, clock.UtcNow, expires,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
