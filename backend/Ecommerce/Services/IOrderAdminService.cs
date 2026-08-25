using Ecommerce.Contracts.Orders;

namespace Ecommerce.Services;

public interface IOrderAdminService
{
    Task<Result<OrdersPageResponse>> GetAllAsync(string? search, OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<AdminOrderDetailResponse>> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<Result<AdminOrderDetailResponse>> UpdateStatusAsync(string orderNumber, OrderStatus status, PaymentStatus paymentStatus, CancellationToken cancellationToken = default);
}
