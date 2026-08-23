using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

// Soft-deleting a product used to be impossible: Remove() was a hard delete and the Restrict
// FKs from Favorites/CartItems blocked it loudly. Now it succeeds, so the dangling references
// it leaves behind have to be dealt with explicitly — Favorite and CartItem are deliberately
// not auditable, so removing them stays a real delete.
public class ProductSoftDeleteCleanupTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<(ApplicationDbContext Context, long ProductId)> SeedAsync()
    {
        var context = CreateContext();

        var category = new Category { Title = "Shoes", Slug = "shoes" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var product = new Product { Title = "Runner", Slug = "runner", Sku = "SKU-1", Price = 10, CategoryId = category.Id };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        context.Favorites.Add(new Favorite { UserId = "user-1", ProductId = product.Id });

        var cart = new Cart { UserId = "user-1" };
        context.Carts.Add(cart);
        await context.SaveChangesAsync();

        context.CartItems.Add(new CartItem { CartId = cart.Id, ProductId = product.Id, Quantity = 2 });
        await context.SaveChangesAsync();

        return (context, product.Id);
    }

    [Fact]
    public async Task Deleting_a_product_drops_it_from_every_favorites_list()
    {
        var (context, productId) = await SeedAsync();
        await using var _ = context;

        var result = await new ProductService(context, new StubFileStorage()).DeleteAsync(productId);

        Assert.True(result.IsSuccess);
        Assert.Empty(await context.Favorites.ToListAsync());
    }

    [Fact]
    public async Task Deleting_a_product_drops_it_from_every_cart()
    {
        var (context, productId) = await SeedAsync();
        await using var _ = context;

        await new ProductService(context, new StubFileStorage()).DeleteAsync(productId);

        Assert.Empty(await context.CartItems.ToListAsync());
    }

    [Fact]
    public async Task The_product_itself_is_only_soft_deleted()
    {
        var (context, productId) = await SeedAsync();
        await using var _ = context;

        await new ProductService(context, new StubFileStorage()).DeleteAsync(productId);

        Assert.Empty(await context.Products.ToListAsync());

        var deleted = await context.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == productId);
        Assert.True(deleted.IsDeleted);
    }

    [Fact]
    public async Task Order_history_still_references_the_deleted_product()
    {
        var (context, productId) = await SeedAsync();
        await using var _ = context;

        context.Orders.Add(new Order { Id = 1, OrderNumber = "ORD-1", UserId = "user-1" });
        await context.SaveChangesAsync();
        context.OrderItems.Add(new OrderItem
        {
            OrderId = 1,
            ProductId = productId,
            ProductTitle = "Runner",
            Sku = "SKU-1",
            UnitPrice = 10,
            Quantity = 1,
            LineTotal = 10,
        });
        await context.SaveChangesAsync();

        await new ProductService(context, new StubFileStorage()).DeleteAsync(productId);

        // Orders snapshot their product details, so history must survive untouched.
        var item = await context.OrderItems.SingleAsync();
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Runner", item.ProductTitle);
    }
}
