namespace HMS.Domain.Common;

public abstract class Entity
{
    public long Id { get; set; }
}

/// <summary>Entity with created/updated audit columns, populated by the audit interceptor.</summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}

/// <summary>Optimistic concurrency token, incremented by the audit interceptor on every update.</summary>
public interface IVersioned
{
    int RowVersion { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}

/// <summary>Changes to entities with this marker are written to audit_logs.</summary>
public interface IAudited;
