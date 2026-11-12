using System.Diagnostics;
using System.Text.Json;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Auditing.Domain;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    private AuditLog(
        Guid id,
        Guid? actorId,
        string actorName,
        string actorRole,
        string action,
        string module,
        string entityType,
        Guid? entityId,
        string summary,
        string? metadata,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        Id = id;
        ActorId = actorId;
        ActorName = actorName;
        ActorRole = actorRole;
        Action = action;
        Module = module;
        EntityType = entityType;
        EntityId = entityId;
        Summary = summary;
        Metadata = metadata;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid? ActorId { get; private set; }

    public string ActorName { get; private set; } = "system";

    public string ActorRole { get; private set; } = "System";

    public string Action { get; private set; } = string.Empty;

    public string Module { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string? Metadata { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditLog Create(
        AuditActor actor,
        string action,
        string module,
        string entityType,
        Guid? entityId,
        string summary,
        object? metadata = null,
        DateTimeOffset? occurredAt = null)
    {
        return new AuditLog(
            Guid.NewGuid(),
            actor.Id,
            actor.Name,
            actor.Role,
            action,
            module,
            entityType,
            entityId,
            summary,
            metadata is null ? null : JsonSerializer.Serialize(metadata, JsonSerializerOptions.Web),
            Activity.Current?.TraceId.ToString(),
            occurredAt ?? DateTimeOffset.UtcNow);
    }
}

public sealed class Actor
{
    private Actor()
    {
    }

    private Actor(Guid id, string displayName, string email, string role, bool isSystem)
    {
        Id = id;
        DisplayName = displayName;
        Email = email;
        Role = role;
        IsSystem = isSystem;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string Role { get; private set; } = "Operations";

    public bool IsSystem { get; private set; }

    public static Actor Create(Guid id, string displayName, string email, string role, bool isSystem)
        => new(id, displayName.Trim(), email.Trim().ToLowerInvariant(), role, isSystem);

    public AuditActor ToAuditActor() => new(Id, DisplayName, Role);
}
