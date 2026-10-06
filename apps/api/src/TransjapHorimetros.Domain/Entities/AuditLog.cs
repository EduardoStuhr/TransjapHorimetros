namespace TransjapHorimetros.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(
        string action,
        string entity,
        string entityId,
        string? oldValue,
        string? newValue,
        DateTimeOffset createdAt,
        string correlationId,
        Guid? userId = null)
    {
        Id = Guid.NewGuid();
        Action = action;
        Entity = entity;
        EntityId = entityId;
        OldValue = oldValue;
        NewValue = newValue;
        CreatedAt = createdAt.ToUniversalTime();
        CorrelationId = correlationId;
        UserId = userId;
    }

    public Guid Id { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string Entity { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public Guid? UserId { get; private set; }
}
