namespace Ecommerce.Contracts.Roles;

public record RoleResponse(long Id, string Name, string? Description, bool IsSystem, List<PermissionResponse> Permissions);
