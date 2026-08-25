using System.Reflection;
using Ecommerce.Authorization;
using Ecommerce.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Tests.Authorization;

public class DashboardControllerAuthorizationTests
{
    [Fact]
    public void AdminDashboardController_requires_the_admin_bearer_scheme()
    {
        var classAuth = typeof(AdminDashboardController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).SingleOrDefault();

        Assert.NotNull(classAuth);
        Assert.Equal(AdminAuthDefaults.Scheme, classAuth!.AuthenticationSchemes);
    }

    [Theory]
    [InlineData("GetSummaryAsync", "DashboardView")]
    [InlineData("GetReportsAsync", "ReportsView")]
    public void AdminDashboardController_actions_require_the_expected_permission(string actionName, string permissionKeyName)
    {
        var action = typeof(AdminDashboardController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;
        var permission = action.GetCustomAttributes<HasPermissionAttribute>(inherit: true).SingleOrDefault();
        var expectedKey = typeof(PermissionKeys).GetField(permissionKeyName, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!.ToString();

        Assert.NotNull(permission);
        Assert.Equal($"{AdminAuthDefaults.PolicyPrefix}{expectedKey}", permission!.Policy);
    }
}
