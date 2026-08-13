using Ecommerce.Entities;
using Ecommerce.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Presistence;

public class SoftDeleteQueryFilterTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    [Fact]
    public async Task Soft_deleted_rows_are_hidden_from_ordinary_queries()
    {
        await using var context = CreateContext();
        context.Categories.Add(new Category { Title = "Live", Slug = "live" });
        context.Categories.Add(new Category { Title = "Gone", Slug = "gone", IsDeleted = true });
        await context.SaveChangesAsync();

        var visible = await context.Categories.ToListAsync();

        Assert.Single(visible);
        Assert.Equal("Live", visible[0].Title);
    }

    [Fact]
    public async Task IgnoreQueryFilters_still_sees_soft_deleted_rows()
    {
        await using var context = CreateContext();
        context.Categories.Add(new Category { Title = "Gone", Slug = "gone", IsDeleted = true });
        await context.SaveChangesAsync();

        var all = await context.Categories.IgnoreQueryFilters().ToListAsync();

        Assert.Single(all);
    }

    [Fact]
    public async Task The_filter_applies_to_customer_accounts_too()
    {
        await using var context = CreateContext();
        context.Users.Add(new ApplicationUser { UserName = "a@b.com", Email = "a@b.com", IsDeleted = true });
        await context.SaveChangesAsync();

        Assert.Empty(await context.Users.ToListAsync());
        Assert.Single(await context.Users.IgnoreQueryFilters().ToListAsync());
    }
}
