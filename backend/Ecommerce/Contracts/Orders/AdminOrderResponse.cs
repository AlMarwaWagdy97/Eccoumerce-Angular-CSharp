namespace Ecommerce.Contracts.Orders;

public record AdminOrderSummaryResponse(
    long Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    string CustomerMobile,
    string Status,
    string PaymentStatus,
    double Total,
    DateTime CreatedOn);

public record OrdersPageResponse(
    IReadOnlyList<AdminOrderSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public record AdminOrderDetailResponse(
    long Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    string CustomerMobile,
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
    DateTime? StatusUpdatedOn,
    IReadOnlyList<OrderItemResponse> Items);
