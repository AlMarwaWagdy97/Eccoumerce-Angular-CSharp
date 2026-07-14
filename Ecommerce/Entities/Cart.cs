namespace Ecommerce.Entities;

public sealed class Cart
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
