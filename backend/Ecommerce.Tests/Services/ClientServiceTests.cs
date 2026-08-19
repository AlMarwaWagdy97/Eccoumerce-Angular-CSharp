using Ecommerce.Contracts.Clients;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Tests.Services;

public class ClientServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext context) => new(
        new UserStore<ApplicationUser>(context),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
        new PasswordHasher<ApplicationUser>(),
        [new UserValidator<ApplicationUser>()],
        [new PasswordValidator<ApplicationUser>()],
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        null!,
        NullLogger<UserManager<ApplicationUser>>.Instance);

    private static async Task<ApplicationUser> SeedClientAsync(
        UserManager<ApplicationUser> userManager,
        string email = "buyer@example.com",
        string firstName = "Bea",
        string lastName = "Buyer")
    {
        var user = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            UserName = email,
            Email = email,
            PhoneNumber = "0100000000",
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user, "Client@123");
        Assert.True(created.Succeeded);
        return user;
    }

    [Fact]
    public async Task GetAllAsync_filters_by_search_term()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        await SeedClientAsync(userManager, "bea@example.com", "Bea", "Buyer");
        await SeedClientAsync(userManager, "carl@example.com", "Carl", "Customer");
        var service = new ClientService(context, userManager);

        var result = await service.GetAllAsync("carl", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal("carl@example.com", result.Value.Items[0].Email);
    }

    [Fact]
    public async Task GetAllAsync_pages_the_result_set()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        await SeedClientAsync(userManager, "a@example.com", "Anna", "One");
        await SeedClientAsync(userManager, "b@example.com", "Bob", "Two");
        await SeedClientAsync(userManager, "c@example.com", "Cara", "Three");
        var service = new ClientService(context, userManager);

        var result = await service.GetAllAsync(null, 2, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task GetByIdAsync_returns_order_count_and_lifetime_total()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager);
        context.Orders.AddRange(
            new Order { OrderNumber = "A-1", UserId = user.Id, SubTotal = 100, ShippingCost = 5, Total = 105 },
            new Order { OrderNumber = "A-2", UserId = user.Id, SubTotal = 40, ShippingCost = 0, Total = 40 });
        await context.SaveChangesAsync();
        var service = new ClientService(context, userManager);

        var result = await service.GetByIdAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.OrderCount);
        Assert.Equal(145d, result.Value.LifetimeTotal);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_fails_for_an_unknown_client()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var service = new ClientService(context, userManager);

        var result = await service.GetByIdAsync(Guid.NewGuid().ToString());

        Assert.False(result.IsSuccess);
        Assert.Equal("Client.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_changes_the_email_and_keeps_the_normalized_values_in_sync()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager, "old@example.com");
        var service = new ClientService(context, userManager);

        var result = await service.UpdateAsync(user.Id, new UpdateClientRequest("Bea", "Updated", "new@example.com", "0111111111"));

        Assert.True(result.IsSuccess);
        Assert.Equal("new@example.com", result.Value.Email);

        var reloaded = await context.Users.FirstAsync(x => x.Id == user.Id);
        Assert.Equal("new@example.com", reloaded.Email);
        Assert.Equal("NEW@EXAMPLE.COM", reloaded.NormalizedEmail);
        Assert.Equal("new@example.com", reloaded.UserName);
        Assert.Equal("NEW@EXAMPLE.COM", reloaded.NormalizedUserName);
        Assert.Equal("Updated", reloaded.LastName);
    }

    [Fact]
    public async Task UpdateAsync_fails_when_the_email_belongs_to_another_client()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager, "first@example.com");
        await SeedClientAsync(userManager, "taken@example.com", "Taken", "Account");
        var service = new ClientService(context, userManager);

        var result = await service.UpdateAsync(user.Id, new UpdateClientRequest("Bea", "Buyer", "taken@example.com", null));

        Assert.False(result.IsSuccess);
        Assert.Equal("Client.EmailAlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task ToggleStatusAsync_disables_and_re_enables_the_account()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager);
        var service = new ClientService(context, userManager);

        Assert.True((await service.ToggleStatusAsync(user.Id)).IsSuccess);

        var disabled = await context.Users.AsNoTracking().FirstAsync(x => x.Id == user.Id);
        Assert.True(disabled.LockoutEnabled);
        Assert.Equal(DateTimeOffset.MaxValue, disabled.LockoutEnd);
        Assert.False((await service.GetByIdAsync(user.Id)).Value.IsActive);

        Assert.True((await service.ToggleStatusAsync(user.Id)).IsSuccess);

        var enabled = await context.Users.AsNoTracking().FirstAsync(x => x.Id == user.Id);
        Assert.Null(enabled.LockoutEnd);
        Assert.True((await service.GetByIdAsync(user.Id)).Value.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_the_client_so_ordinary_queries_skip_them()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager);
        var service = new ClientService(context, userManager);

        var result = await service.DeleteAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Users.AnyAsync(x => x.Id == user.Id));
        Assert.True(await context.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == user.Id));
    }
}
