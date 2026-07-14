namespace Ecommerce.Contracts.Orders;

public record OrderItemResponse(
    long ProductId,
    string ProductTitle,
    string? ProductImage,
    double UnitPrice,
    int Quantity,
    double LineTotal);
