namespace Ecommerce.Authorization;

public class PermissionRequirement(params string[] permissions) : IAuthorizationRequirement
{
    // The user must hold ANY one of these permission claims (OR semantics), not all of them.
    public IReadOnlyList<string> Permissions { get; } = permissions;
}
