namespace Ecommerce.Entities;

public sealed class Review : AuditableEntity
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }

    public Product Product { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
