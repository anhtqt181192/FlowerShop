using FlowerShop.Domain.Common;

namespace FlowerShop.Domain.Orders;

public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Paid = 3,
    Preparing = 4,
    Delivering = 5,
    Completed = 6,
    Cancelled = 7
}

public sealed record ShippingAddress(
    string ReceiverName,
    string Phone,
    string Province,
    string District,
    string Ward,
    string Address);

public sealed class OrderItem
{
    private OrderItem() { }

    internal OrderItem(Guid flowerId, string flowerName, decimal unitPrice, int quantity)
    {
        if (flowerId == Guid.Empty || string.IsNullOrWhiteSpace(flowerName))
            throw new DomainRuleException("Sản phẩm trong đơn hàng không hợp lệ.");
        if (unitPrice <= 0 || quantity <= 0)
            throw new DomainRuleException("Giá và số lượng sản phẩm phải lớn hơn 0.");

        Id = Guid.NewGuid();
        FlowerId = flowerId;
        FlowerName = flowerName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }
    public Guid FlowerId { get; private set; }
    public string FlowerName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal SubTotal => UnitPrice * Quantity;
}

public sealed record OrderItemInfo(Guid FlowerId, string FlowerName, int Quantity);
public sealed record OrderCreatedEvent(Guid OrderId, IReadOnlyCollection<OrderItemInfo> Items);
public sealed record OrderPaidEvent(Guid OrderId, IReadOnlyCollection<OrderItemInfo> Items);
public sealed record OrderCancelledEvent(Guid OrderId, IReadOnlyCollection<OrderItemInfo> Items);

public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderItem> _items = [];
    private Order() { }

    public Order(Guid customerId, ShippingAddress shippingAddress)
    {
        if (customerId == Guid.Empty)
            throw new DomainRuleException("Thông tin khách hàng không hợp lệ.");
        ValidateAddress(shippingAddress);

        Id = Guid.NewGuid();
        OrderNumber = $"FS-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        Status = OrderStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public ShippingAddress ShippingAddress { get; private set; } = new("", "", "", "", "", "");
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
    public DateTime CreatedAt { get; private set; }

    public void AddItem(Guid flowerId, string flowerName, decimal unitPrice, int quantity)
    {
        EnsureEditable();
        var existing = _items.SingleOrDefault(item => item.FlowerId == flowerId);
        if (existing is not null)
        {
            _items.Remove(existing);
            _items.Add(new OrderItem(flowerId, flowerName, unitPrice, existing.Quantity + quantity));
        }
        else
        {
            _items.Add(new OrderItem(flowerId, flowerName, unitPrice, quantity));
        }

        RecalculateTotal();
    }

    public void RemoveItem(Guid flowerId)
    {
        EnsureEditable();
        var item = _items.SingleOrDefault(item => item.FlowerId == flowerId);
        if (item is not null)
            _items.Remove(item);
        RecalculateTotal();
    }

    public void PublishCreated()
    {
        if (_items.Count == 0)
            throw new DomainRuleException("Đơn hàng phải có ít nhất một sản phẩm.");
        RaiseDomainEvent(new OrderCreatedEvent(Id, GetItemInfo()));
    }

    public void Confirm()
    {
        EnsureStatus(OrderStatus.Pending);
        Status = OrderStatus.Confirmed;
    }

    public void MarkPaid()
    {
        EnsureStatus(OrderStatus.Confirmed);
        Status = OrderStatus.Paid;
        RaiseDomainEvent(new OrderPaidEvent(Id, GetItemInfo()));
    }

    public void StartPreparing()
    {
        EnsureStatus(OrderStatus.Paid);
        Status = OrderStatus.Preparing;
    }

    public void StartDelivery()
    {
        EnsureStatus(OrderStatus.Preparing);
        Status = OrderStatus.Delivering;
    }

    public void Complete()
    {
        EnsureStatus(OrderStatus.Delivering);
        Status = OrderStatus.Completed;
    }

    public void Cancel()
    {
        if (Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
            throw new DomainRuleException("Chỉ có thể hủy đơn hàng chưa thanh toán.");
        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelledEvent(Id, GetItemInfo()));
    }

    private IReadOnlyCollection<OrderItemInfo> GetItemInfo() =>
        _items.Select(item => new OrderItemInfo(item.FlowerId, item.FlowerName, item.Quantity)).ToArray();

    private void EnsureEditable()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainRuleException("Không thể thay đổi sản phẩm của đơn hàng này.");
    }

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
            throw new DomainRuleException($"Không thể chuyển đơn hàng từ {Status} sang trạng thái tiếp theo.");
    }

    private void RecalculateTotal() => TotalAmount = _items.Sum(item => item.SubTotal);

    private static void ValidateAddress(ShippingAddress address)
    {
        if (address is null ||
            new[] { address.ReceiverName, address.Phone, address.Province, address.District, address.Ward, address.Address }
                .Any(string.IsNullOrWhiteSpace))
            throw new DomainRuleException("Vui lòng nhập đầy đủ thông tin giao hàng.");
    }
}
