using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class OrderAdminServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<ApplicationUser> SeedUserAsync(ApplicationDbContext context, string email = "dana@example.com")
    {
        var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Dana", LastName = "Diaz" };
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

    private static async Task<Product> SeedProductAsync(ApplicationDbContext context, long categoryId, int stockQuantity = 10)
    {
        var product = new Product { Title = "Runner", Slug = "runner", Sku = "SKU-1", Price = 20, CategoryId = categoryId, StockQuantity = stockQuantity };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private static async Task<Order> SeedOrderAsync(
        ApplicationDbContext context,
        ApplicationUser user,
        Product product,
        string orderNumber = "ORD-0000000001",
        OrderStatus status = OrderStatus.Pending,
        string shipToName = "Dana Diaz",
        string shipToPhone = "01099998888",
        int quantity = 1)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = user.Id,
            User = user,
            Status = status,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            PaymentStatus = PaymentStatus.Pending,
            SubTotal = product.Price * quantity,
            ShippingCost = 5.99,
            Total = product.Price * quantity + 5.99,
            ShipToName = shipToName,
            ShipToPhone = shipToPhone,
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
    public async Task GetAllAsync_search_matches_order_number_name_email_or_mobile()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context, "dana@example.com");
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-ABC123", shipToName: "Dana Diaz", shipToPhone: "01099998888");
        var service = new OrderAdminService(context);

        Assert.Equal(1, (await service.GetAllAsync("ORD-ABC123", null, 1, 20)).Value.TotalCount);
        Assert.Equal(1, (await service.GetAllAsync("dana diaz", null, 1, 20)).Value.TotalCount);
        Assert.Equal(1, (await service.GetAllAsync("dana@example.com", null, 1, 20)).Value.TotalCount);
        Assert.Equal(1, (await service.GetAllAsync("01099998888", null, 1, 20)).Value.TotalCount);
        Assert.Equal(0, (await service.GetAllAsync("nonexistent", null, 1, 20)).Value.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_filters_by_status()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-P1", status: OrderStatus.Pending);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-S1", status: OrderStatus.Shipped);
        var service = new OrderAdminService(context);

        var result = await service.GetAllAsync(null, OrderStatus.Shipped, 1, 20);

        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal("ORD-S1", result.Value.Items[0].OrderNumber);
    }

    [Fact]
    public async Task GetAllAsync_pages_the_result_set()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        for (var i = 1; i <= 3; i++)
            await SeedOrderAsync(context, user, product, orderNumber: $"ORD-{i:0000000000}");
        var service = new OrderAdminService(context);

        var result = await service.GetAllAsync(null, null, 1, 2);

        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Equal(2, result.Value.TotalPages);
    }

    [Fact]
    public async Task GetAllAsync_orders_most_recent_first()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-FIRST");
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-SECOND");
        var service = new OrderAdminService(context);

        var result = await service.GetAllAsync(null, null, 1, 20);

        Assert.Equal("ORD-SECOND", result.Value.Items[0].OrderNumber);
        Assert.Equal("ORD-FIRST", result.Value.Items[1].OrderNumber);
    }

    [Fact]
    public async Task GetByOrderNumberAsync_returns_full_detail_with_customer_info()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context, "dana@example.com");
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-DETAIL", quantity: 2);
        var service = new OrderAdminService(context);

        var result = await service.GetByOrderNumberAsync("ORD-DETAIL");

        Assert.True(result.IsSuccess);
        Assert.Equal("Dana Diaz", result.Value.CustomerName);
        Assert.Equal("dana@example.com", result.Value.CustomerEmail);
        Assert.Equal("01099998888", result.Value.CustomerMobile);
        Assert.Single(result.Value.Items);
        Assert.Equal(2, result.Value.Items[0].Quantity);
    }

    [Fact]
    public async Task GetByOrderNumberAsync_falls_back_when_the_customer_account_was_deleted()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-ORPHAN");

        // Soft-deletes the account — Order.User still points at the row, but the
        // global !IsDeleted filter excludes it from a normal Include.
        context.Users.Remove(user);
        await context.SaveChangesAsync();
        var service = new OrderAdminService(context);

        var result = await service.GetByOrderNumberAsync("ORD-ORPHAN");

        Assert.True(result.IsSuccess);
        Assert.Equal("(deleted account)", result.Value.CustomerEmail);
        Assert.Equal("Dana Diaz", result.Value.CustomerName); // snapshot survives regardless
    }

    [Fact]
    public async Task GetByOrderNumberAsync_fails_for_an_unknown_order_number()
    {
        await using var context = CreateContext();
        var service = new OrderAdminService(context);

        var result = await service.GetByOrderNumberAsync("ORD-MISSING");

        Assert.False(result.IsSuccess);
        Assert.Equal("Order.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateStatusAsync_allows_a_legal_forward_transition()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-FWD", status: OrderStatus.Pending);
        var service = new OrderAdminService(context);

        var result = await service.UpdateStatusAsync("ORD-FWD", OrderStatus.Shipped, PaymentStatus.Pending);

        Assert.True(result.IsSuccess);
        Assert.Equal("Shipped", result.Value.Status);
        Assert.NotNull(result.Value.StatusUpdatedOn);
    }

    [Fact]
    public async Task UpdateStatusAsync_rejects_reverting_to_an_earlier_status()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-REV", status: OrderStatus.Shipped);
        var service = new OrderAdminService(context);

        var result = await service.UpdateStatusAsync("ORD-REV", OrderStatus.Paid, PaymentStatus.Pending);

        Assert.False(result.IsSuccess);
        Assert.Equal("Order.InvalidStatusTransition", result.Error.Code);
    }

    [Theory]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task UpdateStatusAsync_rejects_any_change_once_terminal(OrderStatus terminalStatus)
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-TERM", status: terminalStatus);
        var service = new OrderAdminService(context);

        var result = await service.UpdateStatusAsync("ORD-TERM", OrderStatus.Shipped, PaymentStatus.Pending);

        Assert.False(result.IsSuccess);
        Assert.Equal("Order.InvalidStatusTransition", result.Error.Code);
    }

    [Fact]
    public async Task UpdateStatusAsync_cancelling_restocks_every_line_item()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId, stockQuantity: 7);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-CANCEL", status: OrderStatus.Pending, quantity: 3);
        var service = new OrderAdminService(context);

        var result = await service.UpdateStatusAsync("ORD-CANCEL", OrderStatus.Cancelled, PaymentStatus.Pending);

        Assert.True(result.IsSuccess);
        var restocked = await context.Products.FirstAsync(x => x.Id == product.Id);
        Assert.Equal(10, restocked.StockQuantity);
    }

    [Fact]
    public async Task UpdateStatusAsync_cancelling_restocks_even_when_the_product_was_soft_deleted()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId, stockQuantity: 7);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-CANCEL-DELETED", status: OrderStatus.Pending, quantity: 3);

        context.Products.Remove(product);
        await context.SaveChangesAsync();
        var service = new OrderAdminService(context);

        var result = await service.UpdateStatusAsync("ORD-CANCEL-DELETED", OrderStatus.Cancelled, PaymentStatus.Pending);

        Assert.True(result.IsSuccess);
        var restocked = await context.Products.IgnoreQueryFilters().FirstAsync(x => x.Id == product.Id);
        Assert.Equal(10, restocked.StockQuantity);
    }

    [Fact]
    public async Task UpdateStatusAsync_setting_the_same_status_again_is_a_no_op()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-NOOP", status: OrderStatus.Shipped);
        var service = new OrderAdminService(context);

        var result = await service.UpdateStatusAsync("ORD-NOOP", OrderStatus.Shipped, PaymentStatus.Pending);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.StatusUpdatedOn);
    }

    [Fact]
    public async Task UpdateStatusAsync_updates_payment_status_independently_of_order_status()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, orderNumber: "ORD-PAY", status: OrderStatus.Pending);
        var service = new OrderAdminService(context);

        var result = await service.UpdateStatusAsync("ORD-PAY", OrderStatus.Pending, PaymentStatus.Paid);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.Status);
        Assert.Equal("Paid", result.Value.PaymentStatus);
        Assert.Null(result.Value.StatusUpdatedOn); // OrderStatus untouched, so its timestamp stays untouched too
    }

    [Fact]
    public async Task UpdateStatusAsync_fails_for_an_unknown_order_number()
    {
        await using var context = CreateContext();
        var service = new OrderAdminService(context);

        var result = await service.UpdateStatusAsync("ORD-MISSING", OrderStatus.Shipped, PaymentStatus.Pending);

        Assert.False(result.IsSuccess);
        Assert.Equal("Order.NotFound", result.Error.Code);
    }
}
