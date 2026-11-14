namespace PaymentReconciliation.Modules.Tenancy.Domain;

public sealed class Plan
{
    private Plan()
    {
    }

    private Plan(
        Guid id,
        string code,
        string name,
        string tagline,
        string audience,
        decimal? monthlyPrice,
        string priceNote,
        bool isRecommended,
        int sortOrder)
    {
        Id = id;
        Code = code;
        Name = name;
        Tagline = tagline;
        Audience = audience;
        MonthlyPrice = monthlyPrice;
        PriceNote = priceNote;
        IsRecommended = isRecommended;
        SortOrder = sortOrder;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Tagline { get; private set; } = string.Empty;

    public string Audience { get; private set; } = string.Empty;

    public decimal? MonthlyPrice { get; private set; }

    public string PriceNote { get; private set; } = string.Empty;

    public bool IsRecommended { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public ICollection<PlanFeature> Features { get; private set; } = [];

    public ICollection<PlanLimit> Limits { get; private set; } = [];

    public bool IsFree => MonthlyPrice == 0m;

    public static Plan Create(
        string code,
        string name,
        string tagline,
        string audience,
        decimal? monthlyPrice,
        string priceNote,
        bool isRecommended,
        int sortOrder)
        => new(
            Guid.NewGuid(),
            code.Trim().ToLowerInvariant(),
            name.Trim(),
            tagline.Trim(),
            audience.Trim(),
            monthlyPrice,
            priceNote.Trim(),
            isRecommended,
            sortOrder);

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class Feature
{
    private Feature()
    {
    }

    private Feature(Guid id, string key, string name, string description)
    {
        Id = id;
        Key = key;
        Name = name;
        Description = description;
    }

    public Guid Id { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public static Feature Create(string key, string name, string description)
        => new(Guid.NewGuid(), key.Trim().ToLowerInvariant(), name.Trim(), description.Trim());
}

public sealed class PlanFeature
{
    private PlanFeature()
    {
    }

    private PlanFeature(Guid id, Guid planId, Guid featureId, bool isIncluded, string? note)
    {
        Id = id;
        PlanId = planId;
        FeatureId = featureId;
        IsIncluded = isIncluded;
        Note = note;
    }

    public Guid Id { get; private set; }

    public Guid PlanId { get; private set; }

    public Guid FeatureId { get; private set; }

    public bool IsIncluded { get; private set; }

    public string? Note { get; private set; }

    public Feature? Feature { get; private set; }

    public static PlanFeature Create(Guid planId, Guid featureId, bool isIncluded, string? note = null)
        => new(Guid.NewGuid(), planId, featureId, isIncluded, note);

    public void SetIncluded(bool isIncluded)
        => IsIncluded = isIncluded;
}

public sealed class PlanLimit
{
    private PlanLimit()
    {
    }

    private PlanLimit(Guid id, Guid planId, string key, string label, int? value, string display)
    {
        Id = id;
        PlanId = planId;
        Key = key;
        Label = label;
        Value = value;
        Display = display;
    }

    public Guid Id { get; private set; }

    public Guid PlanId { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public int? Value { get; private set; }

    public string Display { get; private set; } = string.Empty;

    public static PlanLimit Create(Guid planId, string key, string label, int? value, string display)
        => new(Guid.NewGuid(), planId, key.Trim().ToLowerInvariant(), label.Trim(), value, display.Trim());

    public void SetValue(int? value)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Usage limits cannot be negative.");
        }

        Value = value;
        Display = value is null
            ? "Unlimited"
            : Key switch
            {
                "team_members" => $"{value} members",
                "audit_history_days" => $"{value} days",
                _ => $"{value:N0} / month"
            };
    }
}
