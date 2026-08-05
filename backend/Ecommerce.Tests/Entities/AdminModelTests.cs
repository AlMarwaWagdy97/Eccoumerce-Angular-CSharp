using Ecommerce.Entities;
using Ecommerce.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Entities;

public class AdminModelTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new NoopHttpContextAccessor());
    }

    [Fact]
    public async Task Admin_role_and_permissions_round_trip()
    {
        await using var context = CreateContext();

        var permission = new Permission { Key = "products.manage", Module = "Products", Description = "Manage products" };
        var role = new AdminRole { Name = "Manager", Permissions = [permission] };
        var admin = new Admin
        {
            FirstName = "Test",
            LastName = "Admin",
            Email = "test.admin@example.com",
            PasswordHash = "hash",
            AdminRole = role,
        };

        context.Admins.Add(admin);
        await context.SaveChangesAsync();

        var loaded = await context.Admins
            .Include(x => x.AdminRole).ThenInclude(x => x.Permissions)
            .FirstAsync(x => x.Email == "test.admin@example.com");

        Assert.Equal("Manager", loaded.AdminRole.Name);
        Assert.Single(loaded.AdminRole.Permissions);
        Assert.Equal("products.manage", loaded.AdminRole.Permissions[0].Key);
    }
}
