using Ecommerce.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Ecommerce.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext CreateContext(string[] requiredPermissions, params string[] grantedPermissions)
    {
        var claims = grantedPermissions.Select(p => new Claim("permission", p));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement(requiredPermissions);

        return new AuthorizationHandlerContext([requirement], principal, null);
    }

    private static AuthorizationHandlerContext CreateContext(string requiredPermission, params string[] grantedPermissions) =>
        CreateContext([requiredPermission], grantedPermissions);

    [Fact]
    public async Task Succeeds_when_the_user_has_the_required_permission_claim()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = CreateContext("products.manage", "products.manage", "orders.view");

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_when_the_user_is_missing_the_required_permission_claim()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = CreateContext("admins.manage", "products.manage");

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Succeeds_when_the_user_has_any_one_of_multiple_required_permission_claims()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = CreateContext(["roles.manage", "admins.manage"], "admins.manage");

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_when_the_user_has_none_of_multiple_required_permission_claims()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = CreateContext(["roles.manage", "admins.manage"], "products.manage");

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}

public class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider CreateProvider() =>
        new(Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task Single_permission_attribute_round_trips_to_a_single_permission_requirement()
    {
        var policyName = new HasPermissionAttribute(PermissionKeys.RolesManage).Policy!;
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync(policyName);

        var requirement = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Equal([PermissionKeys.RolesManage], requirement.Permissions);
    }

    [Fact]
    public async Task Multi_permission_attribute_round_trips_to_a_single_requirement_carrying_both_keys()
    {
        var policyName = new HasPermissionAttribute(PermissionKeys.RolesManage, PermissionKeys.AdminsManage).Policy!;
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync(policyName);

        var requirement = Assert.Single(policy!.Requirements.OfType<PermissionRequirement>());
        Assert.Equal([PermissionKeys.RolesManage, PermissionKeys.AdminsManage], requirement.Permissions);
    }
}
