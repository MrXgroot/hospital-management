using System.Text.Json;
using HMS.Application.Common;
using HMS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HMS.Infrastructure.Persistence;

/// <summary>
/// Sets audit columns, bumps row_version on updates, and writes audit_logs rows for <see cref="IAudited"/> entities.
/// Registered per DbContext scope (it holds pending insert audits between SavingChanges and SavedChanges).
/// </summary>
public sealed class AuditInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    private static readonly HashSet<string> IgnoredProperties =
        [nameof(AuditableEntity.CreatedAt), nameof(AuditableEntity.CreatedBy), nameof(AuditableEntity.UpdatedAt),
         nameof(AuditableEntity.UpdatedBy), nameof(IVersioned.RowVersion)];

    private static readonly HashSet<string> MaskedProperties = ["PasswordHash"];

    private readonly List<EntityEntry> _pendingInserts = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context) Apply(context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context) Apply(context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _pendingInserts.Count > 0)
        {
            // Inserted rows only have their id after the save, so their audit rows are written in a second save.
            foreach (var entry in _pendingInserts)
                context.Add(NewLog(entry, "Insert", null));
            _pendingInserts.Clear();
            await context.SaveChangesAsync(cancellationToken);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext context)
    {
        var now = clock.UtcNow;
        long? userId = currentUser.IsAuthenticated ? currentUser.UserId : null;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedBy = userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userId;
                }
            }

            if (entry.Entity is IVersioned versioned && entry.State == EntityState.Modified)
                versioned.RowVersion = (int)entry.Property(nameof(IVersioned.RowVersion)).OriginalValue! + 1;

            if (entry.Entity is IAudited)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        _pendingInserts.Add(entry);
                        break;
                    case EntityState.Modified:
                        var changes = ModifiedValues(entry);
                        if (changes.Count > 0) context.Add(NewLog(entry, "Update", changes));
                        break;
                    case EntityState.Deleted:
                        context.Add(NewLog(entry, "Delete", null));
                        break;
                }
            }
        }
    }

    private static Dictionary<string, object?> ModifiedValues(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();
        foreach (var p in entry.Properties)
        {
            if (!p.IsModified || IgnoredProperties.Contains(p.Metadata.Name)) continue;
            if (Equals(p.OriginalValue, p.CurrentValue)) continue;
            changes[p.Metadata.Name] = MaskedProperties.Contains(p.Metadata.Name)
                ? new { old = "***", @new = "***" }
                : new { old = p.OriginalValue, @new = p.CurrentValue };
        }
        return changes;
    }

    private AuditLog NewLog(EntityEntry entry, string action, Dictionary<string, object?>? changes) => new()
    {
        EntityType = entry.Metadata.ClrType.Name,
        EntityId = (long)entry.Property(nameof(Entity.Id)).CurrentValue!,
        Action = action,
        ChangesJson = changes is null ? null : JsonSerializer.Serialize(changes, Json.Options),
        ChangedAt = clock.UtcNow,
        ChangedBy = currentUser.IsAuthenticated ? currentUser.UserId : null,
    };
}
