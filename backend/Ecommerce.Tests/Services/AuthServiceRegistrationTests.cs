using Ecommerce.Authentication;
using Ecommerce.Contracts.Authentication;
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

public class AuthServiceRegistrationTests
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
    public async Task RegisterAsync_rejects_an_email_held_by_a_soft_deleted_account()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var provider = BuildProvider(databaseName);

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var deleted = new ApplicationUser
        {
            UserName = "taken@example.com",
            Email = "taken@example.com",
            NormalizedEmail = "TAKEN@EXAMPLE.COM",
            NormalizedUserName = "TAKEN@EXAMPLE.COM",
            FirstName = "Old",
            LastName = "Account",
            IsDeleted = true,
        };
        var context = provider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(deleted);
        await context.SaveChangesAsync();

        var service = new AuthService(userManager, new Mock<IJwtProvider>().Object);

        var result = await service.RegisterAsync(
            new RegisterRequest("taken@example.com", "Passw0rd!", "New", "User"));

        Assert.False(result.IsSuccess);
        Assert.Equal(UserErrors.DuplicatedEmail.Code, result.Error.Code);
    }
}
