using Ecommerce.Contracts.Roles;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class RoleServiceSoftDeleteTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    [Fact]
    public async Task DeleteAsync_soft_deletes_the_role_instead_of_removing_the_row()
    {
        await using var context = CreateContext();
        var service = new RoleService(context);
        var created = await service.CreateAsync(new RoleRequest("Editor", null, []));

        var result = await service.DeleteAsync(created.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(await context.AdminRoles.ToListAsync());

        var deleted = await context.AdminRoles.IgnoreQueryFilters().SingleAsync(x => x.Id == created.Value.Id);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedOn);
    }

    [Fact]
    public async Task A_deleted_roles_name_can_be_reused()
    {
        await using var context = CreateContext();
        var service = new RoleService(context);
        var created = await service.CreateAsync(new RoleRequest("Editor", null, []));
        await service.DeleteAsync(created.Value.Id);

        var recreated = await service.CreateAsync(new RoleRequest("Editor", null, []));

        Assert.True(recreated.IsSuccess);
    }

    [Fact]
    public async Task GetAllAsync_does_not_return_deleted_roles()
    {
        await using var context = CreateContext();
        var service = new RoleService(context);
        var keep = await service.CreateAsync(new RoleRequest("Keeper", null, []));
        var drop = await service.CreateAsync(new RoleRequest("Doomed", null, []));
        await service.DeleteAsync(drop.Value.Id);

        var all = await service.GetAllAsync();

        Assert.Single(all.Value);
        Assert.Equal(keep.Value.Id, all.Value.First().Id);
    }
}
