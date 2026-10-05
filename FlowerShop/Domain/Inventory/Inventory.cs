using FlowerShop.Domain.Common;

namespace FlowerShop.Domain.Inventory;

public enum InventoryTransactionType
{
    Import = 1,
    Reserve = 2,
    Release = 3,
    Deduct = 4,
    Adjust = 5
}

public sealed class Inventory : AggregateRoot<Guid>
{
    private Inventory() { }

    public Inventory(Guid flowerId, int quantity = 0)
    {
        if (flowerId == Guid.Empty || quantity < 0)
            throw new DomainRuleException("Thông tin tồn kho không hợp lệ.");
        Id = Guid.NewGuid();
        FlowerId = flowerId;
        AvailableQuantity = quantity;
    }

    public Guid FlowerId { get; private set; }
    public int AvailableQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int ActualQuantity => AvailableQuantity - ReservedQuantity;
    public byte[] Version { get; private set; } = [];
    public Catalog.Flower? Flower { get; private set; }

    public void Reserve(int quantity)
    {
        EnsurePositive(quantity);
        if (ActualQuantity < quantity)
            throw new DomainRuleException("Số lượng hoa trong kho không đủ.");
        ReservedQuantity += quantity;
    }

    public void Release(int quantity)
    {
        EnsurePositive(quantity);
        if (ReservedQuantity < quantity)
            throw new DomainRuleException("Số lượng giữ chỗ trong kho không đủ để hoàn lại.");
        ReservedQuantity -= quantity;
    }

    public void Deduct(int quantity)
    {
        EnsurePositive(quantity);
        if (ReservedQuantity < quantity || AvailableQuantity < quantity)
            throw new DomainRuleException("Tồn kho không đủ để hoàn tất thanh toán.");
        AvailableQuantity -= quantity;
        ReservedQuantity -= quantity;
    }

    public void Import(int quantity)
    {
        EnsurePositive(quantity);
        AvailableQuantity += quantity;
    }

    public void Adjust(int quantity)
    {
        if (quantity < ReservedQuantity)
            throw new DomainRuleException("Tồn kho mới không thể thấp hơn số lượng đã giữ chỗ.");
        AvailableQuantity = quantity;
    }

    private static void EnsurePositive(int quantity)
    {
        if (quantity <= 0)
            throw new DomainRuleException("Số lượng phải lớn hơn 0.");
    }
}

public sealed class InventoryTransaction
{
    private InventoryTransaction() { }

    public InventoryTransaction(Guid flowerId, InventoryTransactionType type, int quantity, string referenceNo)
    {
        if (flowerId == Guid.Empty || quantity <= 0 || string.IsNullOrWhiteSpace(referenceNo))
            throw new DomainRuleException("Lịch sử kho không hợp lệ.");
        Id = Guid.NewGuid();
        FlowerId = flowerId;
        Type = type;
        Quantity = quantity;
        ReferenceNo = referenceNo;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid FlowerId { get; private set; }
    public InventoryTransactionType Type { get; private set; }
    public int Quantity { get; private set; }
    public string ReferenceNo { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
}
