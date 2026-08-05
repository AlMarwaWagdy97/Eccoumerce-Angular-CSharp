using Ecommerce.Contracts.Roles;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class RoleServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task SeedPermissionsAsync(ApplicationDbContext context)
    {
        context.Permissions.AddRange(
            new Permission { Key = "products.manage", Module = "Products", Description = "Manage products" },
            new Permission { Key = "orders.view", Module = "Orders", Description = "View orders" });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_creates_a_role_with_the_requested_permissions()
    {
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var service = new RoleService(context);

        var result = await service.CreateAsync(new RoleRequest("Editor", "Can edit products", ["products.manage"]));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Permissions);
        Assert.Equal("products.manage", result.Value.Permissions[0].Key);
    }

    [Fact]
    public async Task CreateAsync_fails_for_a_duplicate_name()
    {
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var service = new RoleService(context);
        await service.CreateAsync(new RoleRequest("Editor", null, []));

        var result = await service.CreateAsync(new RoleRequest("Editor", null, []));

        Assert.False(result.IsSuccess);
        Assert.Equal("Role.NameExists", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_fails_for_an_unknown_permission_key()
    {
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var service = new RoleService(context);

        var result = await service.CreateAsync(new RoleRequest("Editor", null, ["not.a.real.permission"]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Role.UnknownPermissionKey", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_replaces_the_permission_set()
    {
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var service = new RoleService(context);
        var created = (await service.CreateAsync(new RoleRequest("Editor", null, ["products.manage"]))).Value;

        var result = await service.UpdateAsync(created.Id, new RoleRequest("Editor", null, ["orders.view"]));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Permissions);
        Assert.Equal("orders.view", result.Value.Permissions[0].Key);
    }

    [Fact]
    public async Task DeleteAsync_fails_for_a_system_role()
    {
        await using var context = CreateContext();
        context.AdminRoles.Add(new AdminRole { Name = "Super Admin", IsSystem = true });
        await context.SaveChangesAsync();
        var systemRole = await context.AdminRoles.FirstAsync();
        var service = new RoleService(context);

        var result = await service.DeleteAsync(systemRole.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Role.SystemRoleProtected", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_fails_when_the_role_is_still_assigned_to_an_admin()
    {
        await using var context = CreateContext();
        var role = new AdminRole { Name = "Editor" };
        context.AdminRoles.Add(role);
        context.Admins.Add(new Admin { FirstName = "A", LastName = "B", Email = "a@example.com", PasswordHash = "x", AdminRole = role });
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var result = await service.DeleteAsync(role.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Role.InUse", result.Error.Code);
    }
}
