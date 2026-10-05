using FlowerShop.Domain.Common;

namespace FlowerShop.Domain.Payments;

public enum PaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
    VNPay = 3,
    MoMo = 4
}

public enum PaymentStatus
{
    Pending = 1,
    Success = 2,
    Failed = 3
}

public sealed class Payment : AggregateRoot<Guid>
{
    private Payment() { }

    public Payment(Guid orderId, decimal amount, PaymentMethod method)
    {
        if (orderId == Guid.Empty || amount <= 0 || !Enum.IsDefined(method))
            throw new DomainRuleException("Thông tin thanh toán không hợp lệ.");
        Id = Guid.NewGuid();
        OrderId = orderId;
        Amount = amount;
        Method = method;
        Status = PaymentStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? TransactionCode { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }

    public void MarkSuccess(string transactionCode)
    {
        EnsurePending();
        if (string.IsNullOrWhiteSpace(transactionCode))
            throw new DomainRuleException("Mã giao dịch không hợp lệ.");
        Status = PaymentStatus.Success;
        TransactionCode = transactionCode.Trim();
        PaidAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        EnsurePending();
        Status = PaymentStatus.Failed;
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainRuleException("Giao dịch đã được xử lý.");
    }
}
