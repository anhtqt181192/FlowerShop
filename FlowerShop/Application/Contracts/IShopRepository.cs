using FlowerShop.Domain.Catalog;
using FlowerShop.Domain.Customers;
using FlowerShop.Domain.Inventory;
using FlowerShop.Domain.Orders;
using FlowerShop.Domain.Payments;

namespace FlowerShop.Application.Contracts;

public interface IShopRepository
{
    Task<IReadOnlyList<Flower>> GetActiveFlowersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Flower>> GetAllFlowersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<Flower?> GetFlowerAsync(Guid id, CancellationToken cancellationToken);
    Task<Category?> GetCategoryAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Inventory>> GetInventoryAsync(IReadOnlyCollection<Guid> flowerIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<Inventory>> GetAllInventoryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryTransaction>> GetInventoryTransactionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken cancellationToken);
    Task<Order?> GetOrderAsync(Guid id, CancellationToken cancellationToken);
    Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken);
    Task<Inventory?> GetInventoryForFlowerAsync(Guid flowerId, CancellationToken cancellationToken);
    void Add(Customer customer);
    void Add(Order order);
    void Add(Payment payment);
    void Add(Inventory inventory);
    void Add(InventoryTransaction transaction);
    void Add(Flower flower);
    void Add(Category category);
    void Remove(Flower flower);
    void Remove(Category category);
}

public interface IShopUnitOfWork
{
    Task<IShopTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IShopTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
