using FlowerShop.Application;
using FlowerShop.Domain.Common;
using FlowerShop.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace FlowerShop.Api;

public static class ShopEndpoints
{
    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api");

        api.MapGet("/categories", async (ShopApplicationService service, CancellationToken token) =>
            Results.Ok(await service.GetCategoriesAsync(token)));
        api.MapGet("/flowers", async (ShopApplicationService service, CancellationToken token) =>
            Results.Ok(await service.GetFlowersAsync(false, token)));
        api.MapPost("/orders", async (PlaceOrderRequest request, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                var placedOrder = await service.PlaceOrderAsync(request, token);
                return Results.Created($"/api/orders/{placedOrder.Order.Id}", placedOrder);
            }));

        var management = api.MapGroup("/management").RequireAuthorization();
        management.MapGet("/flowers", async (ShopApplicationService service, CancellationToken token) =>
            Results.Ok(await service.GetFlowersAsync(true, token)));
        management.MapPost("/flowers", async (CreateFlowerRequest request, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () => Results.Created("/api/management/flowers",
                await service.CreateFlowerAsync(request, token))));
        management.MapPut("/flowers/{id:guid}", async (Guid id, CreateFlowerRequest request, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                await service.UpdateFlowerAsync(id, request, token);
                return Results.NoContent();
            }));
        management.MapDelete("/flowers/{id:guid}", async (Guid id, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                await service.DeactivateFlowerAsync(id, token);
                return Results.NoContent();
            }));
        management.MapPost("/categories", async (CreateCategoryRequest request, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () => Results.Created("/api/categories",
                await service.CreateCategoryAsync(request, token))));
        management.MapPut("/categories/{id:int}", async (int id, CreateCategoryRequest request, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () => Results.Ok(await service.UpdateCategoryAsync(id, request, token))));
        management.MapDelete("/categories/{id:int}", async (int id, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                await service.DeleteCategoryAsync(id, token);
                return Results.NoContent();
            }));
        management.MapGet("/orders", async (ShopApplicationService service, CancellationToken token) =>
            Results.Ok(await service.GetOrdersAsync(token)));
        management.MapPost("/orders/{id:guid}/cancel", async (Guid id, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                await service.CancelOrderAsync(id, token);
                return Results.NoContent();
            }));
        management.MapPost("/orders/{id:guid}/status", async (Guid id, OrderStatusRequest request, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                await service.AdvanceOrderAsync(id, request.Status, token);
                return Results.NoContent();
            }));
        management.MapPost("/payments/{id:guid}/success", async (Guid id, PaymentResultRequest request, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                await service.MarkPaymentSucceededAsync(id, request.TransactionCode, token);
                return Results.NoContent();
            }));
        management.MapPost("/payments/{id:guid}/failed", async (Guid id, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                await service.MarkPaymentFailedAsync(id, token);
                return Results.NoContent();
            }));
        management.MapGet("/inventory", async (ShopApplicationService service, CancellationToken token) =>
            Results.Ok(await service.GetInventoryAsync(token)));
        management.MapGet("/inventory/transactions", async (ShopApplicationService service, CancellationToken token) =>
            Results.Ok(await service.GetInventoryTransactionsAsync(token)));
        management.MapPost("/inventory/{flowerId:guid}/import", async (
            Guid flowerId, StockAdjustmentRequest request, ShopApplicationService service, CancellationToken token) =>
            await Handle(async () =>
            {
                await service.ImportStockAsync(flowerId, request.Quantity, token);
                return Results.NoContent();
            }));

        return endpoints;
    }

    private static async Task<IResult> Handle(Func<Task<IResult>> operation)
    {
        try
        {
            return await operation();
        }
        catch (DomainRuleException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new { error = "Tồn kho vừa được cập nhật. Vui lòng thử lại." });
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { error = "Dữ liệu bị trùng hoặc đang được sử dụng." });
        }
    }

    public sealed record OrderStatusRequest(OrderStatus Status);
    public sealed record PaymentResultRequest(string TransactionCode);
    public sealed record StockAdjustmentRequest(int Quantity);
}
