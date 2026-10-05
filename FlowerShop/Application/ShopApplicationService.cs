using FlowerShop.Application.Contracts;
using FlowerShop.Domain.Catalog;
using FlowerShop.Domain.Common;
using FlowerShop.Domain.Customers;
using FlowerShop.Domain.Inventory;
using FlowerShop.Domain.Orders;
using FlowerShop.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Application;

public sealed record CategoryDto(int Id, string Name, string? Description);
public sealed record FlowerDto(
    Guid Id, string Name, string? Description, decimal Price, string? ImageUrl,
    int CategoryId, string CategoryName, FlowerStatus Status, int AvailableQuantity);
public sealed record OrderLineRequest(Guid FlowerId, int Quantity);
public sealed record PlaceOrderRequest(
    string FullName, string Phone, string? Email, string? Address,
    string Province, string District, string Ward, string ShippingAddress,
    PaymentMethod PaymentMethod, IReadOnlyCollection<OrderLineRequest> Items);
public sealed record OrderItemDto(Guid FlowerId, string FlowerName, decimal UnitPrice, int Quantity, decimal SubTotal);
public sealed record OrderDto(
    Guid Id, string OrderNumber, string ReceiverName, string Phone, string Province,
    string District, string Ward, string Address, OrderStatus Status, decimal TotalAmount,
    DateTime CreatedAt, IReadOnlyCollection<OrderItemDto> Items);
public sealed record PlacedOrderDto(OrderDto Order, Guid PaymentId, PaymentMethod PaymentMethod);
public sealed record InventoryDto(Guid FlowerId, string FlowerName, int AvailableQuantity, int ReservedQuantity, int ActualQuantity);
public sealed record InventoryTransactionDto(
    Guid Id, Guid FlowerId, InventoryTransactionType Type, int Quantity, string ReferenceNo, DateTime CreatedAt);
public sealed record CreateFlowerRequest(string Name, string? Description, decimal Price, string? ImageUrl, int CategoryId);
public sealed record CreateCategoryRequest(string Name, string? Description);

public sealed class ShopApplicationService(IShopRepository repository, IShopUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        (await repository.GetCategoriesAsync(cancellationToken))
        .Select(category => new CategoryDto(category.Id, category.Name, category.Description)).ToArray();

    public async Task<IReadOnlyList<FlowerDto>> GetFlowersAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var flowers = includeInactive
            ? await repository.GetAllFlowersAsync(cancellationToken)
            : await repository.GetActiveFlowersAsync(cancellationToken);
        var inventory = await repository.GetInventoryAsync(flowers.Select(item => item.Id).ToArray(), cancellationToken);
        var stockByFlower = inventory.ToDictionary(item => item.FlowerId);

        return flowers.Select(flower => new FlowerDto(
            flower.Id,
            flower.Name,
            flower.Description,
            flower.Price,
            flower.ImageUrl,
            flower.CategoryId,
            flower.Category?.Name ?? string.Empty,
            flower.Status,
            stockByFlower.GetValueOrDefault(flower.Id)?.ActualQuantity ?? 0)).ToArray();
    }

    public async Task<PlacedOrderDto> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new DomainRuleException("Giỏ hàng đang trống.");
        if (request.Items.Any(item => item.Quantity <= 0))
            throw new DomainRuleException("Số lượng sản phẩm phải lớn hơn 0.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var flowerIds = request.Items.Select(item => item.FlowerId).Distinct().ToArray();
        var flowers = (await repository.GetActiveFlowersAsync(cancellationToken))
            .Where(flower => flowerIds.Contains(flower.Id)).ToDictionary(flower => flower.Id);
        if (flowers.Count != flowerIds.Length)
            throw new DomainRuleException("Một hoặc nhiều sản phẩm không còn kinh doanh.");

        var inventories = (await repository.GetInventoryAsync(flowerIds, cancellationToken))
            .ToDictionary(item => item.FlowerId);
        var customer = new Customer(request.FullName, request.Phone, request.Email, request.Address);
        var shipping = new ShippingAddress(
            request.FullName.Trim(), request.Phone.Trim(), request.Province.Trim(),
            request.District.Trim(), request.Ward.Trim(), request.ShippingAddress.Trim());
        var order = new Order(customer.Id, shipping);

        foreach (var line in request.Items)
        {
            var flower = flowers[line.FlowerId];
            order.AddItem(flower.Id, flower.Name, flower.Price, line.Quantity);
        }

        order.PublishCreated();
        order.Confirm();
        var payment = new Payment(order.Id, order.TotalAmount, request.PaymentMethod);
        repository.Add(customer);
        repository.Add(order);
        repository.Add(payment);
        await ApplyOrderEventsAsync(order, order.OrderNumber, inventories, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new PlacedOrderDto(MapOrder(order), payment.Id, payment.Method);
    }

    public async Task CancelOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy đơn hàng.");
        order.Cancel();
        var inventories = await repository.GetInventoryAsync(
            order.Items.Select(item => item.FlowerId).ToArray(), cancellationToken);
        await ApplyOrderEventsAsync(order, order.OrderNumber, inventories.ToDictionary(item => item.FlowerId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkPaymentSucceededAsync(Guid paymentId, string transactionCode, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var payment = await repository.GetPaymentAsync(paymentId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy giao dịch.");
        var order = await repository.GetOrderAsync(payment.OrderId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy đơn hàng.");
        payment.MarkSuccess(transactionCode);
        order.MarkPaid();
        var inventories = await repository.GetInventoryAsync(
            order.Items.Select(item => item.FlowerId).ToArray(), cancellationToken);
        await ApplyOrderEventsAsync(order, paymentId.ToString("N"), inventories.ToDictionary(item => item.FlowerId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkPaymentFailedAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await repository.GetPaymentAsync(paymentId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy giao dịch.");
        payment.MarkFailed();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AdvanceOrderAsync(Guid orderId, OrderStatus nextStatus, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy đơn hàng.");
        switch (nextStatus)
        {
            case OrderStatus.Preparing:
                order.StartPreparing();
                break;
            case OrderStatus.Delivering:
                order.StartDelivery();
                break;
            case OrderStatus.Completed:
                order.Complete();
                break;
            default:
                throw new DomainRuleException("Trạng thái đơn hàng không hợp lệ.");
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ImportStockAsync(Guid flowerId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
            throw new DomainRuleException("Số lượng nhập kho phải lớn hơn 0.");
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var flower = await repository.GetFlowerAsync(flowerId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy sản phẩm.");
        var inventory = await repository.GetInventoryForFlowerAsync(flowerId, cancellationToken);
        if (inventory is null)
        {
            inventory = new Inventory(flower.Id);
            repository.Add(inventory);
        }
        inventory.Import(quantity);
        repository.Add(new InventoryTransaction(flowerId, InventoryTransactionType.Import, quantity, "STOCK-IMPORT"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken cancellationToken) =>
        (await repository.GetOrdersAsync(cancellationToken)).Select(MapOrder).ToArray();

    public async Task<IReadOnlyList<InventoryDto>> GetInventoryAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllInventoryAsync(cancellationToken))
        .Select(item => new InventoryDto(
            item.FlowerId, item.Flower?.Name ?? string.Empty,
            item.AvailableQuantity, item.ReservedQuantity, item.ActualQuantity)).ToArray();

    public async Task<IReadOnlyList<InventoryTransactionDto>> GetInventoryTransactionsAsync(
        CancellationToken cancellationToken) =>
        (await repository.GetInventoryTransactionsAsync(cancellationToken))
        .Select(item => new InventoryTransactionDto(
            item.Id, item.FlowerId, item.Type, item.Quantity, item.ReferenceNo, item.CreatedAt)).ToArray();

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = new Category(request.Name, request.Description);
        repository.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task<FlowerDto> CreateFlowerAsync(CreateFlowerRequest request, CancellationToken cancellationToken)
    {
        if (await repository.GetCategoryAsync(request.CategoryId, cancellationToken) is null)
            throw new DomainRuleException("Không tìm thấy danh mục.");
        var flower = new Flower(request.Name, request.Description, request.Price, request.ImageUrl, request.CategoryId);
        repository.Add(flower);
        repository.Add(new Inventory(flower.Id));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetFlowersAsync(true, cancellationToken)).Single(item => item.Id == flower.Id);
    }

    public async Task UpdateFlowerAsync(Guid flowerId, CreateFlowerRequest request, CancellationToken cancellationToken)
    {
        var flower = await repository.GetFlowerAsync(flowerId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy sản phẩm.");
        if (await repository.GetCategoryAsync(request.CategoryId, cancellationToken) is null)
            throw new DomainRuleException("Không tìm thấy danh mục.");
        flower.Update(request.Name, request.Description, request.Price, request.ImageUrl, request.CategoryId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateFlowerAsync(Guid flowerId, CancellationToken cancellationToken)
    {
        var flower = await repository.GetFlowerAsync(flowerId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy sản phẩm.");
        flower.SetStatus(FlowerStatus.Inactive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(
        int categoryId, CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await repository.GetCategoryAsync(categoryId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy danh mục.");
        category.Rename(request.Name);
        category.UpdateDescription(request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task DeleteCategoryAsync(int categoryId, CancellationToken cancellationToken)
    {
        var category = await repository.GetCategoryAsync(categoryId, cancellationToken)
            ?? throw new DomainRuleException("Không tìm thấy danh mục.");
        repository.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyOrderEventsAsync(
        Order order,
        string referenceNo,
        IReadOnlyDictionary<Guid, Inventory> inventories,
        CancellationToken cancellationToken)
    {
        foreach (var domainEvent in order.DomainEvents)
        {
            var (items, type) = domainEvent switch
            {
                OrderCreatedEvent created => (created.Items, InventoryTransactionType.Reserve),
                OrderPaidEvent paid => (paid.Items, InventoryTransactionType.Deduct),
                OrderCancelledEvent cancelled => (cancelled.Items, InventoryTransactionType.Release),
                _ => (Array.Empty<OrderItemInfo>(), (InventoryTransactionType?)null)
            };

            if (type is null)
                continue;

            foreach (var item in items)
            {
                if (!inventories.TryGetValue(item.FlowerId, out var inventory))
                    throw new DomainRuleException("Sản phẩm chưa được thiết lập tồn kho.");
                switch (type.Value)
                {
                    case InventoryTransactionType.Reserve:
                        inventory.Reserve(item.Quantity);
                        break;
                    case InventoryTransactionType.Deduct:
                        inventory.Deduct(item.Quantity);
                        break;
                    case InventoryTransactionType.Release:
                        inventory.Release(item.Quantity);
                        break;
                }
                repository.Add(new InventoryTransaction(item.FlowerId, type.Value, item.Quantity, referenceNo));
            }
        }

        order.ClearDomainEvents();
        await Task.CompletedTask;
    }

    private static OrderDto MapOrder(Order order) =>
        new(
            order.Id,
            order.OrderNumber,
            order.ShippingAddress.ReceiverName,
            order.ShippingAddress.Phone,
            order.ShippingAddress.Province,
            order.ShippingAddress.District,
            order.ShippingAddress.Ward,
            order.ShippingAddress.Address,
            order.Status,
            order.TotalAmount,
            order.CreatedAt,
            order.Items.Select(item => new OrderItemDto(
                item.FlowerId, item.FlowerName, item.UnitPrice, item.Quantity, item.SubTotal)).ToArray());
}
