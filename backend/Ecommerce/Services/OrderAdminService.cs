using Microsoft.EntityFrameworkCore;
using Ecommerce.Contracts.Orders;

namespace Ecommerce.Services;

public class OrderAdminService(ApplicationDbContext context) : IOrderAdminService
{
    private const int MaxPageSize = 100;

    private readonly ApplicationDbContext _context = context;

    public async Task<Result<OrdersPageResponse>> GetAllAsync(string? search, OrderStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var query = _context.Orders.AsNoTracking().Include(x => x.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.OrderNumber.ToLower().Contains(term) ||
                x.ShipToName.ToLower().Contains(term) ||
                x.ShipToPhone.ToLower().Contains(term) ||
                (x.User != null && x.User.Email != null && x.User.Email.ToLower().Contains(term)));
        }

        if (status is not null)
            query = query.Where(x => x.Status == status);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderByDescending(x => x.CreatedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result.Success(new OrdersPageResponse(
            orders.Select(MapSummary).ToList(), page, pageSize, totalCount, totalPages));
    }

    public async Task<Result<AdminOrderDetailResponse>> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
            return Result.Failure<AdminOrderDetailResponse>(OrderErrors.OrderNotFound);

        // Load the user separately with ignored query filters to handle soft-deleted accounts
        if (order.UserId != null)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == order.UserId, cancellationToken);
            order.User = user;
        }

        return Result.Success(MapDetail(order));
    }

    public async Task<Result<AdminOrderDetailResponse>> UpdateStatusAsync(string orderNumber, OrderStatus status, PaymentStatus paymentStatus, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(x => x.User)
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
            return Result.Failure<AdminOrderDetailResponse>(OrderErrors.OrderNotFound);

        if (status != order.Status)
        {
            if (!IsLegalTransition(order.Status, status))
                return Result.Failure<AdminOrderDetailResponse>(OrderErrors.InvalidStatusTransition);

            if (status == OrderStatus.Cancelled)
                await RestockAsync(order, cancellationToken);

            order.Status = status;
            order.StatusUpdatedOn = DateTime.UtcNow;
        }

        order.PaymentStatus = paymentStatus;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(MapDetail(order));
    }

    // OrderStatus is ordinal-ordered (Pending < Paid < Shipped < Delivered); Cancelled is a
    // separate terminal branch reachable from any non-terminal status. Delivered and Cancelled
    // never accept another transition. Equal-status calls never reach this — the caller treats
    // them as a no-op before calling this method.
    private static bool IsLegalTransition(OrderStatus current, OrderStatus next)
    {
        if (current is OrderStatus.Delivered or OrderStatus.Cancelled)
            return false;

        return next == OrderStatus.Cancelled || next > current;
    }

    // Order creation decrements stock and nothing before this method ever restored it —
    // cancelling now puts every line item's quantity back. IgnoreQueryFilters so a product
    // soft-deleted after the order was placed still gets its stock corrected instead of
    // silently skipping it.
    private async Task RestockAsync(Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var item in order.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
                product.StockQuantity += item.Quantity;
        }
    }

    private static AdminOrderSummaryResponse MapSummary(Order order) => new(
        order.Id,
        order.OrderNumber,
        order.ShipToName,
        order.User?.Email ?? "(deleted account)",
        order.ShipToPhone,
        order.Status.ToString(),
        order.PaymentStatus.ToString(),
        order.Total,
        order.CreatedOn);

    private static AdminOrderDetailResponse MapDetail(Order order) => new(
        order.Id,
        order.OrderNumber,
        order.ShipToName,
        order.User?.Email ?? "(deleted account)",
        order.ShipToPhone,
        order.Status.ToString(),
        order.PaymentMethod.ToString(),
        order.PaymentStatus.ToString(),
        order.SubTotal,
        order.ShippingCost,
        order.Total,
        order.ShipToName,
        order.ShipToPhone,
        order.ShipToLine1,
        order.ShipToLine2,
        order.ShipToCity,
        order.ShipToState,
        order.ShipToCountry,
        order.ShipToPostalCode,
        order.CreatedOn,
        order.StatusUpdatedOn,
        order.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductTitle, i.Product?.Image, i.UnitPrice, i.Quantity, i.LineTotal)).ToList());
}
