using Ecommerce.Abstractions;
using Ecommerce.Contracts.Admins;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ecommerce.Tests.Services;

public class AdminServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<AdminRole> SeedRoleAsync(ApplicationDbContext context, string name = "Manager")
    {
        var role = new AdminRole { Name = name };
        context.AdminRoles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static async Task<Admin> SeedAdminAsync(ApplicationDbContext context, AdminRole role, string email = "existing@example.com")
    {
        var admin = new Admin { FirstName = "Existing", LastName = "Admin", Email = email, PasswordHash = "x", AdminRole = role };
        context.Admins.Add(admin);
        await context.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task CreateAsync_creates_the_admin_and_sends_a_set_password_email()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var authService = new Mock<IAdminAuthService>();
        authService.Setup(x => x.ForgotPasswordAsync("new.admin@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var service = new AdminService(context, authService.Object);

        var result = await service.CreateAsync(new CreateAdminRequest("New", "Admin", "new.admin@example.com", null, role.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(role.Name, result.Value.RoleName);
        authService.Verify(x => x.ForgotPasswordAsync("new.admin@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_fails_for_a_duplicate_email()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        await SeedAdminAsync(context, role, "dup@example.com");
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.CreateAsync(new CreateAdminRequest("New", "Admin", "dup@example.com", null, role.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.EmailAlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_fails_for_an_unknown_role()
    {
        await using var context = CreateContext();
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.CreateAsync(new CreateAdminRequest("New", "Admin", "new.admin@example.com", null, 999));

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.RoleNotFound", result.Error.Code);
    }

    [Fact]
    public async Task SetStatusAsync_fails_when_an_admin_tries_to_deactivate_themselves()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var admin = await SeedAdminAsync(context, role);
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.SetStatusAsync(admin.Id, false, currentAdminId: admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.CannotModifyOwnAccount", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_fails_when_an_admin_tries_to_delete_themselves()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var admin = await SeedAdminAsync(context, role);
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.DeleteAsync(admin.Id, currentAdminId: admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.CannotModifyOwnAccount", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_fails_when_an_admin_tries_to_update_themselves()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var admin = await SeedAdminAsync(context, role);
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);
        var request = new UpdateAdminRequest(admin.FirstName, admin.LastName, admin.PhoneNumber, role.Id, admin.IsActive);

        var result = await service.UpdateAsync(admin.Id, request, currentAdminId: admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.CannotModifyOwnAccount", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_succeeds_for_a_different_admin()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var admin = await SeedAdminAsync(context, role);
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.DeleteAsync(admin.Id, currentAdminId: admin.Id + 1);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Admins.AnyAsync(x => x.Id == admin.Id));
    }
}
