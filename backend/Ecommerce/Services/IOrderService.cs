using Ecommerce.Contracts.Orders;

namespace Ecommerce.Services;

public interface IOrderService
{
    Task<Result<OrderResponse>> CreateAsync(string userId, CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<OrderSummaryResponse>>> GetAllAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<OrderResponse>> GetAsync(string userId, string orderNumber, CancellationToken cancellationToken = default);
    Task<Result<OrderTrackingResponse>> GetTrackingAsync(string userId, string orderNumber, CancellationToken cancellationToken = default);
}
