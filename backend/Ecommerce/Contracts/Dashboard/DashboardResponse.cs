namespace Ecommerce.Contracts.Dashboard;

public record DashboardSummaryResponse(
    double TotalRevenue,
    int TotalOrders,
    int ActiveProductCount,
    int ClientCount,
    int LowStockProductCount,
    IReadOnlyList<RecentOrderResponse> RecentOrders);

public record RecentOrderResponse(
    string OrderNumber,
    string CustomerName,
    string Status,
    double Total,
    DateTime CreatedOn);

public record DashboardReportsResponse(
    IReadOnlyList<OrderStatusCountResponse> OrdersByStatus,
    IReadOnlyList<DailyRevenueResponse> RevenueByDay,
    IReadOnlyList<TopProductResponse> TopProducts);

public record OrderStatusCountResponse(string Status, int Count);

public record DailyRevenueResponse(DateOnly Date, int OrderCount, double Revenue);

public record TopProductResponse(long ProductId, string ProductTitle, int QuantitySold, double Revenue);
