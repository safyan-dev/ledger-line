namespace PaymentReconciliation.SharedKernel.Diagnostics;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CurrentValue = new();

    public static string? Current
    {
        get => CurrentValue.Value;
        set => CurrentValue.Value = value;
    }

    public static IDisposable Push(string? correlationId)
    {
        var previous = Current;
        Current = correlationId;
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => Current = previous;
    }
}
