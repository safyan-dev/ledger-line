namespace PaymentReconciliation.Persistence.Abstractions;

public interface ITenantBoundaryBypass
{
    bool IsEnabled { get; }

    IDisposable Begin();
}

public sealed class TenantBoundaryBypass : ITenantBoundaryBypass
{
    private int _depth;

    public bool IsEnabled => _depth > 0;

    public IDisposable Begin()
    {
        _depth++;
        return new Scope(this);
    }

    private sealed class Scope : IDisposable
    {
        private readonly TenantBoundaryBypass _owner;
        private bool _disposed;

        public Scope(TenantBoundaryBypass owner) => _owner = owner;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _owner._depth--;
            _disposed = true;
        }
    }
}
