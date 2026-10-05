namespace FlowerShop.Domain.Common;

public abstract class AggregateRoot<TId>
{
    private readonly List<object> _domainEvents = [];

    public TId Id { get; protected set; } = default!;
    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(object domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

public sealed class DomainRuleException(string message) : Exception(message);
