using System.Reflection;
using Ecommerce.Authorization;
using Ecommerce.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Tests.Authorization;

public class ProductsControllerAuthorizationTests
{
    [Fact]
    public void AdminProductsController_requires_the_admin_bearer_scheme()
    {
        var classAuth = typeof(AdminProductsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).SingleOrDefault();

        Assert.NotNull(classAuth);
        Assert.Equal(AdminAuthDefaults.Scheme, classAuth!.AuthenticationSchemes);
    }

    [Theory]
    [InlineData("GetAllAsync", "ProductsView")]
    [InlineData("GetByIdAsync", "ProductsView")]
    [InlineData("CreateAsync", "ProductsManage")]
    [InlineData("UpdateAsync", "ProductsManage")]
    [InlineData("DeleteAsync", "ProductsManage")]
    [InlineData("ToggleStatusAsync", "ProductsManage")]
    [InlineData("AddImagesAsync", "ProductsManage")]
    [InlineData("DeleteImageAsync", "ProductsManage")]
    public void AdminProductsController_actions_require_the_expected_permission(string actionName, string permissionKeyName)
    {
        var action = typeof(AdminProductsController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;
        var permission = action.GetCustomAttributes<HasPermissionAttribute>(inherit: true).SingleOrDefault();
        var expectedKey = typeof(PermissionKeys).GetField(permissionKeyName, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!.ToString();

        Assert.NotNull(permission);
        Assert.Equal($"{AdminAuthDefaults.PolicyPrefix}{expectedKey}", permission!.Policy);
    }

    [Theory]
    [InlineData("GetAll")]
    [InlineData("Get")]
    public void Public_read_actions_stay_unauthenticated(string actionName)
    {
        var action = typeof(ProductsController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;

        Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Empty(typeof(ProductsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }
}
