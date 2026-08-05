namespace Ecommerce.Entities;

public class AdminRolePermission
{
    public long AdminRoleId { get; set; }
    public AdminRole AdminRole { get; set; } = default!;
    public long PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}
