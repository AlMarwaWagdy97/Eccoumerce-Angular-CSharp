namespace Ecommerce.Entities;

public sealed class Favorite
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long ProductId { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
