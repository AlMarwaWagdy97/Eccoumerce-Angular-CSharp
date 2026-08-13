namespace Ecommerce.Entities;

public sealed class Order : AuditableEntity
{
    public long Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public double SubTotal { get; set; }
    public double ShippingCost { get; set; }
    public double Total { get; set; }

    // Shipping address snapshot (kept on the order so it never changes if the saved Address is edited/deleted)
    public string ShipToName { get; set; } = string.Empty;
    public string ShipToPhone { get; set; } = string.Empty;
    public string ShipToLine1 { get; set; } = string.Empty;
    public string? ShipToLine2 { get; set; }
    public string ShipToCity { get; set; } = string.Empty;
    public string ShipToState { get; set; } = string.Empty;
    public string ShipToCountry { get; set; } = string.Empty;
    public string? ShipToPostalCode { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
