using System.Diagnostics;
using System.Text.Json;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Auditing.Domain;

public sealed class PlatformAuditLog
{
    private PlatformAuditLog()
    {
    }

    private PlatformAuditLog(
        Guid id,
        Guid? actorId,
        string actorName,
        string? actorEmail,
        string actorRole,
        string action,
        string targetType,
        Guid? targetId,
        Guid? tenantId,
        string summary,
        string? metadata,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        Id = id;
        ActorId = actorId;
        ActorName = actorName;
        ActorEmail = actorEmail;
        ActorRole = actorRole;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        TenantId = tenantId;
        Summary = summary;
        Metadata = metadata;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid? ActorId { get; private set; }

    public string ActorName { get; private set; } = "system";

    public string? ActorEmail { get; private set; }

    public string ActorRole { get; private set; } = "PlatformAdmin";

    public string Action { get; private set; } = string.Empty;

    public string TargetType { get; private set; } = string.Empty;

    public Guid? TargetId { get; private set; }

    public Guid? TenantId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string? Metadata { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static PlatformAuditLog Create(
        AuditActor actor,
        string? actorEmail,
        string action,
        string targetType,
        Guid? targetId,
        Guid? tenantId,
        string summary,
        object? metadata = null,
        DateTimeOffset? occurredAt = null)
    {
        return new PlatformAuditLog(
            Guid.NewGuid(),
            actor.Id,
            actor.Name,
            actorEmail,
            actor.Role,
            action,
            targetType,
            targetId,
            tenantId,
            summary,
            metadata is null ? null : JsonSerializer.Serialize(metadata, JsonSerializerOptions.Web),
            Activity.Current?.TraceId.ToString(),
            occurredAt ?? DateTimeOffset.UtcNow);
    }
}
