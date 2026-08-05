using Ecommerce.Authentication;
using Ecommerce.Email;
using Ecommerce.Entities;
using Ecommerce.Options;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

    private static Mock<IEmailSender> CreateEmailSenderMock() => new();

    private static IOptions<FrontendOptions> CreateFrontendOptions() =>
        Microsoft.Extensions.Options.Options.Create(new FrontendOptions { AdminAppUrl = "http://localhost:4200/admin" });

    private static AdminAuthService CreateService(ApplicationDbContext context, Mock<IAdminJwtProvider>? jwtProvider = null, Mock<IEmailSender>? emailSender = null) =>
        new(context, (jwtProvider ?? CreateJwtProviderMock()).Object, (emailSender ?? CreateEmailSenderMock()).Object, CreateFrontendOptions());

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
        var service = CreateService(context, jwtProvider);

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
        var service = CreateService(context);

        var result = await service.LoginAsync("test.admin@example.com", "Wrong#123");

        Assert.False(result.IsSuccess);
        Assert.Equal("AdminAuth.InvalidCredentials", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_fails_for_a_deactivated_admin()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123", isActive: false);
        var service = CreateService(context);

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
        var service = CreateService(context);

        var result = await service.LogoutAsync(admin.Id);

        Assert.True(result.IsSuccess);
        var reloaded = await context.Admins.FirstAsync(x => x.Id == admin.Id);
        Assert.All(reloaded.RefreshTokens, rt => Assert.False(rt.IsActive));
    }

    [Fact]
    public async Task ForgotPasswordAsync_sends_a_reset_email_for_a_known_address()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123");
        var emailSender = CreateEmailSenderMock();
        var service = CreateService(context, emailSender: emailSender);

        var result = await service.ForgotPasswordAsync("test.admin@example.com");

        Assert.True(result.IsSuccess);
        emailSender.Verify(x => x.SendAsync(
            "test.admin@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("http://localhost:4200/admin")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_succeeds_silently_for_an_unknown_address()
    {
        await using var context = CreateContext();
        var emailSender = CreateEmailSenderMock();
        var service = CreateService(context, emailSender: emailSender);

        var result = await service.ForgotPasswordAsync("nobody@example.com");

        Assert.True(result.IsSuccess);
        emailSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_updates_the_password_and_revokes_refresh_tokens_for_a_valid_token()
    {
        await using var context = CreateContext();
        var admin = await SeedAdminAsync(context, "OldPass#123");
        admin.RefreshTokens.Add(new AdminRefreshToken { Token = "rt-1", ExpiresOn = DateTime.UtcNow.AddDays(14) });
        await context.SaveChangesAsync();
        var service = CreateService(context);
        await service.ForgotPasswordAsync("test.admin@example.com");
        var issuedToken = (await context.Admins.FirstAsync(x => x.Id == admin.Id)).PasswordResetTokens.Single().Token;

        var result = await service.ResetPasswordAsync("test.admin@example.com", issuedToken, "NewPass#456");

        Assert.True(result.IsSuccess);
        var reloaded = await context.Admins.FirstAsync(x => x.Id == admin.Id);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<Admin>().VerifyHashedPassword(reloaded, reloaded.PasswordHash, "NewPass#456"));
        Assert.All(reloaded.RefreshTokens, rt => Assert.False(rt.IsActive));
    }

    [Fact]
    public async Task ResetPasswordAsync_fails_for_an_unknown_or_reused_token()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "OldPass#123");
        var service = CreateService(context);

        var result = await service.ResetPasswordAsync("test.admin@example.com", "not-a-real-token", "NewPass#456");

        Assert.False(result.IsSuccess);
        Assert.Equal("AdminAuth.InvalidResetToken", result.Error.Code);
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
