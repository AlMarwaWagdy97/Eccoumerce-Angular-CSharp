namespace Ecommerce.Contracts.Authentication;

public record OrderSummaryResponse(
    long Id,
    string OrderNumber,
    string Status,
    double Total,
    DateTime CreatedOn,
    string? TrackingNumber);
