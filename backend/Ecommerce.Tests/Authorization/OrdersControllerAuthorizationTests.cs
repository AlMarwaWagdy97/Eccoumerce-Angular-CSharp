using System.Reflection;
using Ecommerce.Authorization;
using Ecommerce.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Tests.Authorization;

public class OrdersControllerAuthorizationTests
{
    [Fact]
    public void AdminOrdersController_requires_the_admin_bearer_scheme()
    {
        var classAuth = typeof(AdminOrdersController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).SingleOrDefault();

        Assert.NotNull(classAuth);
        Assert.Equal(AdminAuthDefaults.Scheme, classAuth!.AuthenticationSchemes);
    }

    [Theory]
    [InlineData("GetAllAsync", "OrdersView")]
    [InlineData("GetByOrderNumberAsync", "OrdersView")]
    [InlineData("UpdateStatusAsync", "OrdersManage")]
    public void AdminOrdersController_actions_require_the_expected_permission(string actionName, string permissionKeyName)
    {
        var action = typeof(AdminOrdersController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;
        var permission = action.GetCustomAttributes<HasPermissionAttribute>(inherit: true).SingleOrDefault();
        var expectedKey = typeof(PermissionKeys).GetField(permissionKeyName, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!.ToString();

        Assert.NotNull(permission);
        Assert.Equal($"{AdminAuthDefaults.PolicyPrefix}{expectedKey}", permission!.Policy);
    }
}
