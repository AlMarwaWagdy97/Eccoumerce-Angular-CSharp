using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class DashboardServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<ApplicationUser> SeedUserAsync(ApplicationDbContext context, string email = "buyer@example.com")
    {
        var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Bea", LastName = "Buyer" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<long> SeedCategoryAsync(ApplicationDbContext context)
    {
        var category = new Category { Title = "Shoes", Slug = "shoes" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<Product> SeedProductAsync(
        ApplicationDbContext context, long categoryId, string title = "Runner", string sku = "SKU-1",
        int stockQuantity = 10, bool status = true)
    {
        var product = new Product
        {
            Title = title,
            Slug = title.ToLower(),
            Sku = sku,
            Price = 20,
            CategoryId = categoryId,
            StockQuantity = stockQuantity,
            Status = status
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private static async Task<Order> SeedOrderAsync(
        ApplicationDbContext context,
        ApplicationUser user,
        Product product,
        string orderNumber,
        OrderStatus status = OrderStatus.Pending,
        DateTime? createdOn = null,
        int quantity = 1)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = user.Id,
            User = user,
            Status = status,
            CreatedOn = createdOn ?? DateTime.UtcNow,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            PaymentStatus = PaymentStatus.Pending,
            SubTotal = product.Price * quantity,
            ShippingCost = 5.99,
            Total = product.Price * quantity + 5.99,
            ShipToName = "Bea Buyer",
            ShipToPhone = "01000000000",
            ShipToLine1 = "1 Main St",
            ShipToCity = "Cairo",
            ShipToState = "Cairo",
            ShipToCountry = "EG",
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    Product = product,
                    ProductTitle = product.Title,
                    Sku = product.Sku,
                    UnitPrice = product.Price,
                    Quantity = quantity,
                    LineTotal = product.Price * quantity
                }
            }
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task GetSummaryAsync_computes_total_revenue_excluding_cancelled_orders()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-1", status: OrderStatus.Delivered);
        await SeedOrderAsync(context, user, product, "ORD-2", status: OrderStatus.Cancelled);
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Price + 5.99, result.Value.TotalRevenue, precision: 2);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_all_orders_regardless_of_status()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-1", status: OrderStatus.Pending);
        await SeedOrderAsync(context, user, product, "ORD-2", status: OrderStatus.Cancelled);
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(2, result.Value.TotalOrders);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_only_active_products()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        await SeedProductAsync(context, categoryId, title: "Active", sku: "SKU-A", status: true);
        await SeedProductAsync(context, categoryId, title: "Inactive", sku: "SKU-B", status: false);
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(1, result.Value.ActiveProductCount);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_clients()
    {
        await using var context = CreateContext();
        await SeedUserAsync(context, "a@example.com");
        await SeedUserAsync(context, "b@example.com");
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(2, result.Value.ClientCount);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_low_stock_products()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        await SeedProductAsync(context, categoryId, title: "Low", sku: "SKU-A", stockQuantity: 3);
        await SeedProductAsync(context, categoryId, title: "High", sku: "SKU-B", stockQuantity: 50);
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(1, result.Value.LowStockProductCount);
    }

    [Fact]
    public async Task GetSummaryAsync_returns_the_five_most_recent_orders_newest_first()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        var now = DateTime.UtcNow;
        for (var i = 1; i <= 6; i++)
            await SeedOrderAsync(context, user, product, $"ORD-{i}", createdOn: now.AddDays(-i));
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(5, result.Value.RecentOrders.Count);
        Assert.Equal("ORD-1", result.Value.RecentOrders[0].OrderNumber);
    }

    [Fact]
    public async Task GetSummaryAsync_returns_zeros_and_empty_lists_for_an_empty_database()
    {
        await using var context = CreateContext();
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalRevenue);
        Assert.Equal(0, result.Value.TotalOrders);
        Assert.Equal(0, result.Value.ActiveProductCount);
        Assert.Equal(0, result.Value.ClientCount);
        Assert.Equal(0, result.Value.LowStockProductCount);
        Assert.Empty(result.Value.RecentOrders);
    }

    [Fact]
    public async Task GetReportsAsync_counts_orders_by_status_including_zero_for_missing_statuses()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-1", status: OrderStatus.Pending);
        await SeedOrderAsync(context, user, product, "ORD-2", status: OrderStatus.Pending);
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        Assert.Equal(5, result.Value.OrdersByStatus.Count);
        Assert.Equal(2, result.Value.OrdersByStatus.Single(x => x.Status == "Pending").Count);
        Assert.Equal(0, result.Value.OrdersByStatus.Single(x => x.Status == "Delivered").Count);
    }

    [Fact]
    public async Task GetReportsAsync_fills_revenue_by_day_for_the_last_7_days_including_zero_days()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-TODAY", createdOn: DateTime.UtcNow);
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        Assert.Equal(7, result.Value.RevenueByDay.Count);
        var today = result.Value.RevenueByDay[^1];
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), today.Date);
        Assert.Equal(1, today.OrderCount);
        var yesterday = result.Value.RevenueByDay[^2];
        Assert.Equal(0, yesterday.OrderCount);
        Assert.Equal(0, yesterday.Revenue);
    }

    [Fact]
    public async Task GetReportsAsync_excludes_cancelled_orders_from_revenue_by_day()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-CANCELLED", status: OrderStatus.Cancelled, createdOn: DateTime.UtcNow);
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        var today = result.Value.RevenueByDay[^1];
        Assert.Equal(0, today.OrderCount);
        Assert.Equal(0, today.Revenue);
    }

    [Fact]
    public async Task GetReportsAsync_returns_the_top_products_by_revenue_excluding_cancelled_orders()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var popular = await SeedProductAsync(context, categoryId, title: "Popular", sku: "SKU-POP");
        var cancelledOnly = await SeedProductAsync(context, categoryId, title: "CancelledOnly", sku: "SKU-CAN");
        await SeedOrderAsync(context, user, popular, "ORD-1", quantity: 3);
        await SeedOrderAsync(context, user, cancelledOnly, "ORD-2", status: OrderStatus.Cancelled, quantity: 5);
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        Assert.Single(result.Value.TopProducts);
        Assert.Equal("Popular", result.Value.TopProducts[0].ProductTitle);
        Assert.Equal(3, result.Value.TopProducts[0].QuantitySold);
    }

    [Fact]
    public async Task GetReportsAsync_returns_empty_reports_for_an_empty_database()
    {
        await using var context = CreateContext();
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.OrdersByStatus.Count);
        Assert.All(result.Value.OrdersByStatus, x => Assert.Equal(0, x.Count));
        Assert.Equal(7, result.Value.RevenueByDay.Count);
        Assert.All(result.Value.RevenueByDay, x => Assert.Equal(0, x.OrderCount));
        Assert.Empty(result.Value.TopProducts);
    }
}
