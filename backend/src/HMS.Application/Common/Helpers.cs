using System.Text.Json;
using System.Text.Json.Serialization;
using HMS.Domain.Common;
using HMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Common;

public static class DbErrors
{
    /// <summary>True when the save failed on the given unique index (MySQL error 1062 mentions the key name).</summary>
    public static bool IsUniqueViolation(DbUpdateException ex, string indexName)
    {
        var message = ex.InnerException?.Message ?? "";
        return message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
               && message.Contains(indexName, StringComparison.OrdinalIgnoreCase);
    }
}

public static class CurrentUserExtensions
{
    public static bool IsAdmin(this ICurrentUser user) => user.Role == Role.Admin;

    /// <summary>Only the owning doctor (or an admin) may change a doctor-owned clinical record.</summary>
    public static void EnsureOwnsClinicalRecord(this ICurrentUser user, long doctorId)
    {
        if (user.IsAdmin()) return;
        if (user.Role != Role.Doctor || user.DoctorId != doctorId)
            throw new ForbiddenException("Only the consulting doctor can change this record.");
    }

    public static void EnsureRowVersion(this IVersioned entity, int? expected)
    {
        if (expected is { } v && v != entity.RowVersion)
            throw new ConflictException("CONCURRENCY_CONFLICT",
                "This record was changed by someone else. Reload and try again.");
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
