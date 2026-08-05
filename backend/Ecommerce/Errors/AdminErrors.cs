namespace Ecommerce.Errors;

public static class AdminErrors
{
    public static readonly Error AdminNotFound = new("Admin.NotFound", "No admin was found with the given ID");
    public static readonly Error EmailAlreadyExists = new("Admin.EmailAlreadyExists", "Another admin with this email already exists");
    public static readonly Error RoleNotFound = new("Admin.RoleNotFound", "The selected role does not exist");
    public static readonly Error CannotModifyOwnAccount = new("Admin.CannotModifyOwnAccount", "You cannot deactivate or delete your own account");
}
