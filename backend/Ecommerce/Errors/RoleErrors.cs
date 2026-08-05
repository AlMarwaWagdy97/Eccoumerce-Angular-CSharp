namespace Ecommerce.Errors;

public static class RoleErrors
{
    public static readonly Error RoleNotFound = new("Role.NotFound", "No role was found with the given ID");
    public static readonly Error RoleNameExists = new("Role.NameExists", "Another role with the same name already exists");
    public static readonly Error SystemRoleProtected = new("Role.SystemRoleProtected", "Built-in system roles cannot be edited or deleted");
    public static readonly Error UnknownPermissionKey = new("Role.UnknownPermissionKey", "One or more permission keys do not exist");
    public static readonly Error RoleInUse = new("Role.InUse", "This role is still assigned to one or more admins");
}
