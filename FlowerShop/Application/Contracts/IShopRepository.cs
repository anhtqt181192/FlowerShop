using FlowerShop.Domain.Catalog;
using FlowerShop.Domain.Customers;
using FlowerShop.Domain.Inventory;
using FlowerShop.Domain.Orders;
using FlowerShop.Domain.Payments;

namespace FlowerShop.Application.Contracts;

public interface ICustomerRepository
{
    void Add(Customer customer);
}

public interface IFlowerRepository
{
    Task<IReadOnlyList<Flower>> GetActiveFlowersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Flower>> GetAllFlowersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken);
    Task<Flower?> GetFlowerAsync(Guid id, CancellationToken cancellationToken);
    Task<Category?> GetCategoryAsync(int id, CancellationToken cancellationToken);
    void Add(Flower flower);
    void Add(Category category);
    void Remove(Flower flower);
    void Remove(Category category);
}

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken cancellationToken);
    Task<Order?> GetOrderAsync(Guid id, CancellationToken cancellationToken);
    void Add(Order order);
}

public interface IInventoryRepository
{
    Task<IReadOnlyList<Inventory>> GetInventoryAsync(IReadOnlyCollection<Guid> flowerIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<Inventory>> GetAllInventoryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryTransaction>> GetInventoryTransactionsAsync(CancellationToken cancellationToken);
    Task<Inventory?> GetInventoryForFlowerAsync(Guid flowerId, CancellationToken cancellationToken);
    void Add(Inventory inventory);
    void Add(InventoryTransaction transaction);
}

public interface IPaymentRepository
{
    Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken);
    void Add(Payment payment);
}

public interface IShopRepository : ICustomerRepository, IFlowerRepository, IOrderRepository,
    IInventoryRepository, IPaymentRepository { }

public interface IShopUnitOfWork
{
    Task<IShopTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IShopTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
