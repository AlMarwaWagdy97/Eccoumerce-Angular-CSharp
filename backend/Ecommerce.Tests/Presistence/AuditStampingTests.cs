using System.Security.Claims;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Presistence;

public class AuditStampingTests
{
    private static IHttpContextAccessor AccessorFor(string? nameIdentifier)
    {
        var accessor = new NoopHttpContextAccessor();
        if (nameIdentifier is null)
            return accessor;

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, nameIdentifier)], "TestAuth");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return accessor;
    }

    private static ApplicationDbContext CreateContext(string databaseName, string? nameIdentifier) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(databaseName).Options,
        AccessorFor(nameIdentifier));

    [Fact]
    public async Task An_admin_request_stamps_CreatedById()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString(), "7");
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        var category = await context.Categories.SingleAsync();
        Assert.Equal(7, category.CreatedById);
    }

    [Fact]
    public async Task A_customer_request_leaves_the_admin_audit_columns_null()
    {
        // A customer id is a GUID string; it must never be written into an Admin foreign key.
        await using var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        var category = await context.Categories.SingleAsync();
        Assert.Null(category.CreatedById);
    }

    [Fact]
    public async Task An_anonymous_request_leaves_the_admin_audit_columns_null()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString(), null);
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        Assert.Null((await context.Categories.SingleAsync()).CreatedById);
    }

    [Fact]
    public async Task An_update_stamps_UpdatedById_and_UpdatedOn()
    {
        var database = Guid.NewGuid().ToString();
        await using var context = CreateContext(database, "7");
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        var category = await context.Categories.SingleAsync();
        category.Title = "Boots";
        await context.SaveChangesAsync();

        Assert.Equal(7, category.UpdatedById);
        Assert.NotNull(category.UpdatedOn);
    }

    [Fact]
    public async Task Remove_becomes_a_soft_delete()
    {
        var database = Guid.NewGuid().ToString();
        await using var context = CreateContext(database, "7");
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        context.Categories.Remove(await context.Categories.SingleAsync());
        await context.SaveChangesAsync();

        Assert.Empty(await context.Categories.ToListAsync());

        var deleted = await context.Categories.IgnoreQueryFilters().SingleAsync();
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedOn);
        Assert.Equal(7, deleted.DeletedById);
    }
}
