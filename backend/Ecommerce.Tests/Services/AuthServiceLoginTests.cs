using Ecommerce.Authentication;
using Ecommerce.Entities;
using Ecommerce.Errors;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ecommerce.Tests.Services;

public class AuthServiceLoginTests
{
    private static ServiceProvider BuildProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new NoopHttpContextAccessor());
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetTokenAsync_rejects_a_client_disabled_via_the_admin_lockout_toggle()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var provider = BuildProvider(databaseName);
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = "locked@example.com",
            Email = "locked@example.com",
            FirstName = "Locked",
            LastName = "Client",
        };
        Assert.True((await userManager.CreateAsync(user, "Passw0rd!")).Succeeded);

        // Mirrors ClientService.ToggleStatusAsync's disable branch exactly.
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);

        var service = new AuthService(userManager, new Mock<IJwtProvider>().Object);

        var result = await service.GetTokenAsync("locked@example.com", "Passw0rd!");

        Assert.False(result.IsSuccess);
        Assert.Equal(UserErrors.AccountLocked.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetTokenAsync_succeeds_once_the_admin_lockout_toggle_is_reverted()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var provider = BuildProvider(databaseName);
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = "reenabled@example.com",
            Email = "reenabled@example.com",
            FirstName = "Reenabled",
            LastName = "Client",
        };
        Assert.True((await userManager.CreateAsync(user, "Passw0rd!")).Succeeded);

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);

        // Mirrors ClientService.ToggleStatusAsync's re-enable branch exactly.
        user.LockoutEnd = null;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);

        var service = new AuthService(userManager, new Mock<IJwtProvider>().Object);

        var result = await service.GetTokenAsync("reenabled@example.com", "Passw0rd!");

        Assert.True(result.IsSuccess);
    }
}
