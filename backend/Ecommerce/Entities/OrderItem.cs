namespace Ecommerce.Entities;

public sealed class OrderItem : AuditableEntity
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }

    // Snapshotted product details so historical orders are unaffected by later product changes
    public string ProductTitle { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public double UnitPrice { get; set; }
    public int Quantity { get; set; }
    public double LineTotal { get; set; }

    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
