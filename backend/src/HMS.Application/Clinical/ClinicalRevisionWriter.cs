using System.Text.Json;
using HMS.Application.Common;
using HMS.Domain.Clinical;
using Microsoft.EntityFrameworkCore;

namespace HMS.Application.Clinical;

public sealed record FieldChangeDto(string Field, string? OldValue, string? NewValue);

public sealed record RevisionDto(
    long Id, ClinicalEntityType EntityType, long EntityId, int RevisionNumber, RevisionChangeType ChangeType,
    string? Reason, DateTime ChangedAt, string? ChangedByName, IReadOnlyList<FieldChangeDto> Changes);

/// <summary>Writes append-only snapshots of clinical records and reads them back as field-level diffs.</summary>
public sealed class ClinicalRevisionWriter(IAppDbContext db, IClock clock, ICurrentUser currentUser)
{
    /// <summary>
    /// Adds a revision row (not saved). Returns false and adds nothing for an Updated/Amended change
    /// that didn't actually change any field.
    /// </summary>
    public async Task<bool> AddAsync<TSnapshot>(
        ClinicalEntityType type, long entityId, long consultationId, RevisionChangeType changeType,
        string? reason, TSnapshot snapshot, CancellationToken ct)
    {
        var previous = await db.ClinicalRecordRevisions.AsNoTracking()
            .Where(r => r.EntityType == type && r.EntityId == entityId)
            .OrderByDescending(r => r.RevisionNumber)
            .Select(r => new { r.RevisionNumber, r.SnapshotJson })
            .FirstOrDefaultAsync(ct);

        var json = JsonSerializer.Serialize(snapshot, Json.Options);
        var changed = previous is null ? [] : Diff(previous.SnapshotJson, json).Select(c => c.Field).ToList();
        if (previous is not null && changed.Count == 0 && changeType is RevisionChangeType.Updated or RevisionChangeType.Amended)
            return false;

        db.ClinicalRecordRevisions.Add(new ClinicalRecordRevision
        {
            EntityType = type, EntityId = entityId, ConsultationId = consultationId,
            RevisionNumber = (previous?.RevisionNumber ?? 0) + 1, ChangeType = changeType, Reason = reason,
            SnapshotJson = json, ChangedFields = changed.Count > 0 ? JsonSerializer.Serialize(changed) : null,
            ChangedAt = clock.UtcNow, ChangedBy = currentUser.UserId,
        });
        return true;
    }

    public async Task<IReadOnlyList<RevisionDto>> ListAsync(
        long consultationId, ClinicalEntityType? type, long? entityId, CancellationToken ct)
    {
        var q = db.ClinicalRecordRevisions.AsNoTracking().Where(r => r.ConsultationId == consultationId);
        if (type is { } t) q = q.Where(r => r.EntityType == t);
        if (entityId is { } e) q = q.Where(r => r.EntityId == e);
        var rows = await q.OrderBy(r => r.ChangedAt).ThenBy(r => r.Id).ToListAsync(ct);

        var userIds = rows.Select(r => r.ChangedBy).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var lastSnapshot = new Dictionary<(ClinicalEntityType, long), string>();
        var result = new List<RevisionDto>();
        foreach (var r in rows)
        {
            var key = (r.EntityType, r.EntityId);
            var changes = lastSnapshot.TryGetValue(key, out var prev) ? Diff(prev, r.SnapshotJson) : [];
            lastSnapshot[key] = r.SnapshotJson;
            result.Add(new RevisionDto(r.Id, r.EntityType, r.EntityId, r.RevisionNumber, r.ChangeType, r.Reason,
                r.ChangedAt, names.GetValueOrDefault(r.ChangedBy), changes));
        }
        result.Reverse(); // newest first
        return result;
    }

    /// <summary>Top-level property diff of two JSON objects. Nested values are compared as raw JSON.</summary>
    public static IReadOnlyList<FieldChangeDto> Diff(string oldJson, string newJson)
    {
        using var oldDoc = JsonDocument.Parse(oldJson);
        using var newDoc = JsonDocument.Parse(newJson);
        var oldProps = oldDoc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
        var newProps = newDoc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

        var changes = new List<FieldChangeDto>();
        foreach (var name in oldProps.Keys.Union(newProps.Keys))
        {
            var o = oldProps.TryGetValue(name, out var ov) ? Display(ov) : null;
            var n = newProps.TryGetValue(name, out var nv) ? Display(nv) : null;
            if (o != n) changes.Add(new FieldChangeDto(name, o, n));
        }
        return changes;
    }

    private static string? Display(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => e.GetString(),
        _ => e.GetRawText(),
    };
}
