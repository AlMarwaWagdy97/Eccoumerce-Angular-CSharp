namespace Ecommerce.Contracts.Orders;

public record OrderResponse(
    long Id,
    string OrderNumber,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    double SubTotal,
    double ShippingCost,
    double Total,
    string ShipToName,
    string ShipToPhone,
    string ShipToLine1,
    string? ShipToLine2,
    string ShipToCity,
    string ShipToState,
    string ShipToCountry,
    string? ShipToPostalCode,
    DateTime CreatedOn,
    IReadOnlyList<OrderItemResponse> Items);
