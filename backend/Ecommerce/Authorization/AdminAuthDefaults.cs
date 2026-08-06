namespace Ecommerce.Authorization;

public static class AdminAuthDefaults
{
    public const string Scheme = "AdminBearer";
    public const string PolicyPrefix = "Permission:";

    // Permission keys are lowercase.dot.separated (see PermissionKeys), so a comma can never
    // collide with a real key — safe to use as the delimiter for OR'd multi-permission policies.
    public const string PermissionDelimiter = ",";
}
