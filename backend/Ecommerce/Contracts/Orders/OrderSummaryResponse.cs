namespace Ecommerce.Contracts.Orders;

public record OrderSummaryResponse(
    long Id,
    string OrderNumber,
    string Status,
    double Total,
    DateTime CreatedOn);
