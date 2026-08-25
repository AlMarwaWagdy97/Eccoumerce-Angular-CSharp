using Microsoft.EntityFrameworkCore;
using Ecommerce.Contracts.Dashboard;

namespace Ecommerce.Services;

public class DashboardService(ApplicationDbContext context) : IDashboardService
{
    private const int LowStockThreshold = 5;
    private const int RevenueByDayWindow = 7;
    private const int RecentOrdersCount = 5;
    private const int TopProductsCount = 5;

    private readonly ApplicationDbContext _context = context;

    public async Task<Result<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var totalRevenue = await _context.Orders.AsNoTracking()
            .Where(x => x.Status != OrderStatus.Cancelled)
            .SumAsync(x => x.Total, cancellationToken);

        var totalOrders = await _context.Orders.AsNoTracking().CountAsync(cancellationToken);

        var activeProductCount = await _context.Products.AsNoTracking()
            .Where(x => x.Status)
            .CountAsync(cancellationToken);

        var clientCount = await _context.Users.AsNoTracking().CountAsync(cancellationToken);

        var lowStockProductCount = await _context.Products.AsNoTracking()
            .Where(x => x.StockQuantity <= LowStockThreshold)
            .CountAsync(cancellationToken);

        var recentOrders = await _context.Orders.AsNoTracking()
            .OrderByDescending(x => x.CreatedOn)
            .Take(RecentOrdersCount)
            .Select(x => new RecentOrderResponse(x.OrderNumber, x.ShipToName, x.Status.ToString(), x.Total, x.CreatedOn))
            .ToListAsync(cancellationToken);

        return Result.Success(new DashboardSummaryResponse(
            totalRevenue, totalOrders, activeProductCount, clientCount, lowStockProductCount, recentOrders));
    }

    public async Task<Result<DashboardReportsResponse>> GetReportsAsync(CancellationToken cancellationToken = default)
    {
        var ordersByStatus = await GetOrdersByStatusAsync(cancellationToken);
        var revenueByDay = await GetRevenueByDayAsync(cancellationToken);
        var topProducts = await GetTopProductsAsync(cancellationToken);

        return Result.Success(new DashboardReportsResponse(ordersByStatus, revenueByDay, topProducts));
    }

    private async Task<IReadOnlyList<OrderStatusCountResponse>> GetOrdersByStatusAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.Orders.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Enum.GetValues<OrderStatus>()
            .Select(status => new OrderStatusCountResponse(
                status.ToString(),
                counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0))
            .ToList();
    }

    // Fetches only the narrow (CreatedOn, Total) shape for the window, then groups/fills
    // missing days in memory — EF Core's DateOnly-from-DateTime grouping doesn't translate
    // cleanly, and the row count here is always tiny (a handful of days of orders).
    private async Task<IReadOnlyList<DailyRevenueResponse>> GetRevenueByDayAsync(CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.Date.AddDays(-(RevenueByDayWindow - 1));

        var recentOrders = await _context.Orders.AsNoTracking()
            .Where(x => x.Status != OrderStatus.Cancelled && x.CreatedOn >= since)
            .Select(x => new { x.CreatedOn, x.Total })
            .ToListAsync(cancellationToken);

        var byDay = recentOrders
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedOn))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(x => x.Total)));

        var sinceDate = DateOnly.FromDateTime(since);
        return Enumerable.Range(0, RevenueByDayWindow)
            .Select(offset => sinceDate.AddDays(offset))
            .Select(date => byDay.TryGetValue(date, out var value)
                ? new DailyRevenueResponse(date, value.Count, value.Revenue)
                : new DailyRevenueResponse(date, 0, 0))
            .ToList();
    }

    // Filters via a subquery on Order.Id rather than accessing OrderItem.Order.Status
    // directly — avoids relying on required-navigation predicate translation, the same
    // defensive posture Phase 4 landed on after its Include-on-a-filtered-navigation defect.
    private async Task<IReadOnlyList<TopProductResponse>> GetTopProductsAsync(CancellationToken cancellationToken)
    {
        var nonCancelledOrderIds = _context.Orders.AsNoTracking()
            .Where(x => x.Status != OrderStatus.Cancelled)
            .Select(x => x.Id);

        var items = await _context.OrderItems.AsNoTracking()
            .Where(x => nonCancelledOrderIds.Contains(x.OrderId))
            .Select(x => new { x.ProductId, x.ProductTitle, x.Quantity, x.LineTotal })
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(x => new { x.ProductId, x.ProductTitle })
            .Select(g => new TopProductResponse(g.Key.ProductId, g.Key.ProductTitle, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .OrderByDescending(x => x.Revenue)
            .Take(TopProductsCount)
            .ToList();
    }
}
