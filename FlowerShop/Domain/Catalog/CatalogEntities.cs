using FlowerShop.Domain.Common;

namespace FlowerShop.Domain.Catalog;

public enum FlowerStatus
{
    Active = 1,
    Inactive = 2
}

public sealed class Category : AggregateRoot<int>
{
    private Category() { }

    public Category(string name, string? description = null)
    {
        Id = 0;
        Rename(name);
        Description = description?.Trim();
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("Tên danh mục không được để trống.");

        Name = name.Trim();
    }

    public void UpdateDescription(string? description) => Description = description?.Trim();
}

public sealed class Flower : AggregateRoot<Guid>
{
    private Flower() { }

    public Flower(string name, string? description, decimal price, string? imageUrl, int categoryId)
    {
        Id = Guid.NewGuid();
        Update(name, description, price, imageUrl, categoryId);
        Status = FlowerStatus.Active;
    }

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Price { get; private set; }
    public string? ImageUrl { get; private set; }
    public int CategoryId { get; private set; }
    public FlowerStatus Status { get; private set; }
    public Category? Category { get; private set; }

    public void Update(string name, string? description, decimal price, string? imageUrl, int categoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainRuleException("Tên hoa không được để trống.");
        if (price <= 0)
            throw new DomainRuleException("Giá hoa phải lớn hơn 0.");
        if (categoryId <= 0)
            throw new DomainRuleException("Danh mục không hợp lệ.");

        Name = name.Trim();
        Description = description?.Trim();
        Price = price;
        ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        CategoryId = categoryId;
    }

    public void SetStatus(FlowerStatus status) => Status = status;
}
