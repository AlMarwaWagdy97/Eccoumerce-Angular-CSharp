namespace Ecommerce.Entities;

public class Permission
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public List<AdminRole> AdminRoles { get; set; } = [];
}
