using FlowerShop.Domain.Common;

namespace FlowerShop.Domain.Customers;

public sealed class Customer : AggregateRoot<Guid>
{
    private Customer() { }

    public Customer(string fullName, string phone, string? email, string? address)
    {
        Id = Guid.NewGuid();
        UpdateContact(fullName, phone, email, address);
        CreatedAt = DateTime.UtcNow;
    }

    public string FullName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void UpdateContact(string fullName, string phone, string? email, string? address)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainRuleException("Tên khách hàng không được để trống.");
        if (string.IsNullOrWhiteSpace(phone))
            throw new DomainRuleException("Số điện thoại không được để trống.");

        FullName = fullName.Trim();
        Phone = phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }
}
