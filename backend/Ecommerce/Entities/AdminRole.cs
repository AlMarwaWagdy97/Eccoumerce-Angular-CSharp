namespace Ecommerce.Entities;

public class AdminRole : AuditableEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }

    public List<Permission> Permissions { get; set; } = [];
    public List<Admin> Admins { get; set; } = [];
}
