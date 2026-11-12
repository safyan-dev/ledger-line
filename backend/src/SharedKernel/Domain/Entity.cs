namespace PaymentReconciliation.SharedKernel.Domain;

public abstract class Entity
{
    protected Entity()
        => Id = Guid.NewGuid();

    protected Entity(Guid id)
        => Id = id;

    public Guid Id { get; protected set; }

    public override bool Equals(object? obj)
        => obj is Entity other
           && GetType() == other.GetType()
           && Id == other.Id;

    public override int GetHashCode()
        => HashCode.Combine(GetType(), Id);
}
