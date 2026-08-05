using Ecommerce.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Ecommerce.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext CreateContext(string requiredPermission, params string[] grantedPermissions)
    {
        var claims = grantedPermissions.Select(p => new Claim("permission", p));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement(requiredPermission);

        return new AuthorizationHandlerContext([requirement], principal, null);
    }

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
}
