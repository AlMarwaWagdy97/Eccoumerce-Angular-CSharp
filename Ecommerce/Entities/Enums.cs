namespace Ecommerce.Entities;

public enum OrderStatus
{
    Pending = 0,
    Paid = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}

public enum PaymentMethod
{
    CashOnDelivery = 0,
    Card = 1
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2
}
