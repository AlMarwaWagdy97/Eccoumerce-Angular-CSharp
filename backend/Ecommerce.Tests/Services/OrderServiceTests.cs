using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class OrderServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<ApplicationUser> SeedUserAsync(ApplicationDbContext context, string email = "tracking@example.com")
    {
        var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Tara", LastName = "Tracker" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Order> SeedOrderAsync(
        ApplicationDbContext context,
        ApplicationUser user,
        string orderNumber,
        OrderStatus status,
        DateTime? statusUpdatedOn = null)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = user.Id,
            User = user,
            Status = status,
            StatusUpdatedOn = statusUpdatedOn,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            PaymentStatus = PaymentStatus.Pending,
            SubTotal = 20,
            ShippingCost = 5.99,
            Total = 25.99,
            ShipToName = "Tara Tracker",
            ShipToPhone = "01000000000",
            ShipToLine1 = "1 Main St",
            ShipToCity = "Cairo",
            ShipToState = "Cairo",
            ShipToCountry = "EG",
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task GetTrackingAsync_shows_no_date_for_the_current_step_when_StatusUpdatedOn_was_never_set()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedOrderAsync(context, user, "ORD-PREMIGRATION", OrderStatus.Shipped, statusUpdatedOn: null);
        var service = new OrderService(context);

        var result = await service.GetTrackingAsync(user.Id, order.OrderNumber);

        Assert.True(result.IsSuccess);
        var shippedStep = result.Value.Steps.Single(s => s.Status == "Shipped");
        Assert.True(shippedStep.IsCurrent);
        Assert.Null(shippedStep.CompletedOn);
    }

    [Fact]
    public async Task GetTrackingAsync_shows_the_real_timestamp_once_an_admin_has_updated_the_status()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var updatedOn = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);
        var order = await SeedOrderAsync(context, user, "ORD-UPDATED", OrderStatus.Shipped, statusUpdatedOn: updatedOn);
        var service = new OrderService(context);

        var result = await service.GetTrackingAsync(user.Id, order.OrderNumber);

        Assert.True(result.IsSuccess);
        var shippedStep = result.Value.Steps.Single(s => s.Status == "Shipped");
        Assert.Equal(updatedOn, shippedStep.CompletedOn);
    }

    [Fact]
    public async Task GetTrackingAsync_shows_the_cancellation_timestamp_for_a_cancelled_order()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var cancelledOn = new DateTime(2026, 8, 21, 9, 0, 0, DateTimeKind.Utc);
        var order = await SeedOrderAsync(context, user, "ORD-CANCELLED", OrderStatus.Cancelled, statusUpdatedOn: cancelledOn);
        var service = new OrderService(context);

        var result = await service.GetTrackingAsync(user.Id, order.OrderNumber);

        Assert.True(result.IsSuccess);
        var cancelledStep = result.Value.Steps.Single(s => s.Status == "Cancelled");
        Assert.Equal(cancelledOn, cancelledStep.CompletedOn);
    }
}
