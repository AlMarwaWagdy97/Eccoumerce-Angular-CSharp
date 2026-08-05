using Ecommerce.Authentication;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ecommerce.Tests.Services;

public class AdminAuthServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static Mock<IAdminJwtProvider> CreateJwtProviderMock() =>
        new Mock<IAdminJwtProvider>()
            .Also(m => m.Setup(x => x.GenerateToken(It.IsAny<Admin>(), It.IsAny<IEnumerable<string>>()))
                        .Returns(("fake-jwt", 1800)));

    private static async Task<Admin> SeedAdminAsync(ApplicationDbContext context, string password, bool isActive = true)
    {
        var role = new AdminRole { Name = "Manager", Permissions = [new Permission { Key = "products.manage", Module = "Products", Description = "x" }] };
        var admin = new Admin { FirstName = "Test", LastName = "Admin", Email = "test.admin@example.com", AdminRole = role, IsActive = isActive };
        admin.PasswordHash = new PasswordHasher<Admin>().HashPassword(admin, password);

        context.Admins.Add(admin);
        await context.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task LoginAsync_returns_success_with_permissions_for_correct_credentials()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123");
        var jwtProvider = CreateJwtProviderMock();
        var service = new AdminAuthService(context, jwtProvider.Object);

        var result = await service.LoginAsync("test.admin@example.com", "Correct#123");

        Assert.True(result.IsSuccess);
        Assert.Equal("Manager", result.Value.RoleName);
        Assert.Contains("products.manage", result.Value.Permissions);
    }

    [Fact]
    public async Task LoginAsync_fails_for_wrong_password()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123");
        var service = new AdminAuthService(context, CreateJwtProviderMock().Object);

        var result = await service.LoginAsync("test.admin@example.com", "Wrong#123");

        Assert.False(result.IsSuccess);
        Assert.Equal("AdminAuth.InvalidCredentials", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_fails_for_a_deactivated_admin()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123", isActive: false);
        var service = new AdminAuthService(context, CreateJwtProviderMock().Object);

        var result = await service.LoginAsync("test.admin@example.com", "Correct#123");

        Assert.False(result.IsSuccess);
        Assert.Equal("AdminAuth.AccountInactive", result.Error.Code);
    }

    [Fact]
    public async Task LogoutAsync_revokes_all_active_refresh_tokens()
    {
        await using var context = CreateContext();
        var admin = await SeedAdminAsync(context, "Correct#123");
        admin.RefreshTokens.Add(new AdminRefreshToken { Token = "rt-1", ExpiresOn = DateTime.UtcNow.AddDays(14) });
        await context.SaveChangesAsync();
        var service = new AdminAuthService(context, CreateJwtProviderMock().Object);

        var result = await service.LogoutAsync(admin.Id);

        Assert.True(result.IsSuccess);
        var reloaded = await context.Admins.FirstAsync(x => x.Id == admin.Id);
        Assert.All(reloaded.RefreshTokens, rt => Assert.False(rt.IsActive));
    }
}

internal static class MockExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
