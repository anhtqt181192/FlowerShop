using FlowerShop.Application.Contracts;
using FlowerShop.Domain.Catalog;
using FlowerShop.Domain.Customers;
using FlowerShop.Domain.Inventory;
using FlowerShop.Domain.Orders;
using FlowerShop.Domain.Payments;
using FlowerShop.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Infrastructure.Persistence;

public sealed class EfShopRepository(ApplicationDbContext dbContext) : IShopRepository, IShopUnitOfWork
{
    public async Task<IReadOnlyList<Flower>> GetActiveFlowersAsync(CancellationToken cancellationToken) =>
        await dbContext.Flowers.AsNoTracking().Include(flower => flower.Category)
            .Where(flower => flower.Status == FlowerStatus.Active)
            .OrderBy(flower => flower.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Flower>> GetAllFlowersAsync(CancellationToken cancellationToken) =>
        await dbContext.Flowers.AsNoTracking().Include(flower => flower.Category)
            .OrderBy(flower => flower.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        await dbContext.Categories.AsNoTracking().OrderBy(category => category.Name).ToListAsync(cancellationToken);

    public Task<Flower?> GetFlowerAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Flowers.SingleOrDefaultAsync(flower => flower.Id == id, cancellationToken);

    public Task<Category?> GetCategoryAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Categories.SingleOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Inventory>> GetInventoryAsync(
        IReadOnlyCollection<Guid> flowerIds, CancellationToken cancellationToken) =>
        await dbContext.Inventories.Where(inventory => flowerIds.Contains(inventory.FlowerId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Inventory>> GetAllInventoryAsync(CancellationToken cancellationToken) =>
        await dbContext.Inventories.AsNoTracking().Include(inventory => inventory.Flower)
            .OrderBy(inventory => inventory.Flower!.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InventoryTransaction>> GetInventoryTransactionsAsync(
        CancellationToken cancellationToken) =>
        await dbContext.InventoryTransactions.AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .Take(100).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Order>> GetOrdersAsync(CancellationToken cancellationToken) =>
        await dbContext.Orders.AsNoTracking().Include("_items")
            .OrderByDescending(order => order.CreatedAt).Take(100).ToListAsync(cancellationToken);

    public Task<Order?> GetOrderAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Orders.Include("_items").SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    public Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Payments.SingleOrDefaultAsync(payment => payment.Id == id, cancellationToken);

    public Task<Inventory?> GetInventoryForFlowerAsync(Guid flowerId, CancellationToken cancellationToken) =>
        dbContext.Inventories.SingleOrDefaultAsync(inventory => inventory.FlowerId == flowerId, cancellationToken);

    public void Add(Customer customer) => dbContext.Customers.Add(customer);
    public void Add(Order order) => dbContext.Orders.Add(order);
    public void Add(Payment payment) => dbContext.Payments.Add(payment);
    public void Add(Inventory inventory) => dbContext.Inventories.Add(inventory);
    public void Add(InventoryTransaction transaction) => dbContext.InventoryTransactions.Add(transaction);
    public void Add(Flower flower) => dbContext.Flowers.Add(flower);
    public void Add(Category category) => dbContext.Categories.Add(category);
    public void Remove(Flower flower) => dbContext.Flowers.Remove(flower);
    public void Remove(Category category) => dbContext.Categories.Remove(category);

    public async Task<IShopTransaction> BeginTransactionAsync(CancellationToken cancellationToken) =>
        new EfShopTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private sealed class EfShopTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
        : IShopTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
