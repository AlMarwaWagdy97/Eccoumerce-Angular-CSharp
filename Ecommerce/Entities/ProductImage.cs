namespace Ecommerce.Entities;

public sealed class ProductImage
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Sort { get; set; }

    public Product Product { get; set; } = null!;
}
