namespace Ecommerce.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(params string[] permissions)
    {
        Policy = $"{AdminAuthDefaults.PolicyPrefix}{string.Join(AdminAuthDefaults.PermissionDelimiter, permissions)}";
    }
}
