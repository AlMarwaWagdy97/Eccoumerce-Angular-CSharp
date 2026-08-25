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

        var query = _context.Orders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            // Resolved as a plain, un-joined query against Users so a soft-deleted
            // account's global filter can never silently drop matching Order rows —
            // Order.User is a required navigation and querying through it here would.
            var matchingUserIds = await _context.Users.AsNoTracking()
                .Where(u => u.Email != null && u.Email.ToLower().Contains(term))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            query = query.Where(x =>
                x.OrderNumber.ToLower().Contains(term) ||
                x.ShipToName.ToLower().Contains(term) ||
                x.ShipToPhone.ToLower().Contains(term) ||
                matchingUserIds.Contains(x.UserId));
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

        var userIds = orders.Select(x => x.UserId).Distinct().ToList();
        var users = await _context.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var items = orders.Select(o => MapSummary(o, users.TryGetValue(o.UserId, out var u) ? u.Email : null)).ToList();

        return Result.Success(new OrdersPageResponse(items, page, pageSize, totalCount, totalPages));
    }

    public async Task<Result<AdminOrderDetailResponse>> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
            return Result.Failure<AdminOrderDetailResponse>(OrderErrors.OrderNotFound);

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == order.UserId, cancellationToken);

        return Result.Success(MapDetail(order, user?.Email));
    }

    public async Task<Result<AdminOrderDetailResponse>> UpdateStatusAsync(string orderNumber, OrderStatus status, PaymentStatus paymentStatus, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
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

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == order.UserId, cancellationToken);
        return Result.Success(MapDetail(order, user?.Email));
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

    private static AdminOrderSummaryResponse MapSummary(Order order, string? customerEmail) => new(
        order.Id,
        order.OrderNumber,
        order.ShipToName,
        customerEmail ?? "(deleted account)",
        order.ShipToPhone,
        order.Status.ToString(),
        order.PaymentStatus.ToString(),
        order.Total,
        order.CreatedOn);

    private static AdminOrderDetailResponse MapDetail(Order order, string? customerEmail) => new(
        order.Id,
        order.OrderNumber,
        order.ShipToName,
        customerEmail ?? "(deleted account)",
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
