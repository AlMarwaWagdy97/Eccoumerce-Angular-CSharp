namespace Ecommerce.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = $"{AdminAuthDefaults.PolicyPrefix}{permission}";
    }
}
