using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Presistence;

// Development-only seed data so the account pages (Orders/Favorites/Addresses/Cards)
// have something to display without requiring a manual checkout first.
public static class DataSeeder
{
    public const string SeedUserEmail = "seed.tester@example.com";
    public const string SeedUserPassword = "SeedTester@123";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Products in this DB currently have zero stock, which would block every
        // add-to-cart/checkout flow. Ensure there's purchasable stock to test against.
        var outOfStockProducts = await context.Products.Where(x => x.StockQuantity <= 0).ToListAsync();
        foreach (var product in outOfStockProducts)
            product.StockQuantity = 50;
        if (outOfStockProducts.Count > 0)
            await context.SaveChangesAsync();

        var user = await userManager.FindByEmailAsync(SeedUserEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = SeedUserEmail,
                Email = SeedUserEmail,
                EmailConfirmed = true,
                FirstName = "Seed",
                LastName = "Tester"
            };

            var createResult = await userManager.CreateAsync(user, SeedUserPassword);
            if (!createResult.Succeeded)
                return;
        }

        var products = await context.Products.Take(3).ToListAsync();
        if (products.Count == 0)
            return;

        if (!await context.Addresses.AnyAsync(x => x.UserId == user.Id))
        {
            context.Addresses.Add(new Address
            {
                UserId = user.Id,
                FullName = "Seed Tester",
                Phone = "+1 555-0100",
                Line1 = "123 Market Street",
                City = "Springfield",
                State = "IL",
                Country = "USA",
                PostalCode = "62701",
                IsDefault = true
            });
        }

        if (!await context.Cards.AnyAsync(x => x.UserId == user.Id))
        {
            context.Cards.Add(new Card
            {
                UserId = user.Id,
                CardholderName = "Seed Tester",
                Brand = "Visa",
                Last4 = "4242",
                ExpiryMonth = 12,
                ExpiryYear = DateTime.UtcNow.Year + 2,
                IsDefault = true
            });
        }

        if (!await context.Favorites.AnyAsync(x => x.UserId == user.Id))
        {
            foreach (var product in products.Take(2))
                context.Favorites.Add(new Favorite { UserId = user.Id, ProductId = product.Id });
        }

        if (!await context.Orders.AnyAsync(x => x.UserId == user.Id))
        {
            var sampleOrders = new[]
            {
                (Status: OrderStatus.Delivered, DaysAgo: 14),
                (Status: OrderStatus.Shipped, DaysAgo: 3),
                (Status: OrderStatus.Pending, DaysAgo: 0)
            };

            foreach (var sample in sampleOrders)
            {
                var product = products[Random.Shared.Next(products.Count)];
                var unitPrice = product.PriceAfterSale ?? product.Price;
                const int quantity = 1;
                var lineTotal = unitPrice * quantity;
                var shipping = lineTotal >= 50 ? 0 : 5.99;
                var createdOn = DateTime.UtcNow.AddDays(-sample.DaysAgo);

                context.Orders.Add(new Order
                {
                    OrderNumber = $"ORD-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
                    UserId = user.Id,
                    Status = sample.Status,
                    PaymentMethod = PaymentMethod.CashOnDelivery,
                    PaymentStatus = sample.Status == OrderStatus.Delivered ? PaymentStatus.Paid : PaymentStatus.Pending,
                    SubTotal = lineTotal,
                    ShippingCost = shipping,
                    Total = lineTotal + shipping,
                    ShipToName = "Seed Tester",
                    ShipToPhone = "+1 555-0100",
                    ShipToLine1 = "123 Market Street",
                    ShipToCity = "Springfield",
                    ShipToState = "IL",
                    ShipToCountry = "USA",
                    ShipToPostalCode = "62701",
                    CreatedOn = createdOn,
                    Items =
                    [
                        new OrderItem
                        {
                            ProductId = product.Id,
                            ProductTitle = product.Title,
                            Sku = product.Sku,
                            UnitPrice = unitPrice,
                            Quantity = quantity,
                            LineTotal = lineTotal
                        }
                    ]
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
