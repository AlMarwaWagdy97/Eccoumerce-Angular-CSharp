using System.Reflection;
using Ecommerce.Authorization;
using Ecommerce.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Tests.Authorization;

public class ProductsControllerAuthorizationTests
{
    [Theory]
    [InlineData("Add")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("ToggleStatus")]
    public void Write_actions_require_the_products_manage_permission(string actionName)
    {
        var action = typeof(ProductsController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;

        var permission = action.GetCustomAttributes<HasPermissionAttribute>(inherit: true).SingleOrDefault();

        Assert.NotNull(permission);
        Assert.Equal($"{AdminAuthDefaults.PolicyPrefix}{PermissionKeys.ProductsManage}", permission!.Policy);
        Assert.Equal(AdminAuthDefaults.Scheme, permission.AuthenticationSchemes);
    }

    [Theory]
    [InlineData("GetAll")]
    [InlineData("Get")]
    public void Read_actions_stay_public_for_the_storefront(string actionName)
    {
        var action = typeof(ProductsController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;

        Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Empty(typeof(ProductsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }
}
