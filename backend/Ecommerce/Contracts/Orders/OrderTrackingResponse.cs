namespace Ecommerce.Contracts.Orders;

public record OrderTrackingStep(
    string Status,
    string Label,
    bool IsCompleted,
    bool IsCurrent,
    DateTime? CompletedOn);

public record OrderTrackingResponse(
    string OrderNumber,
    string Status,
    DateTime CreatedOn,
    IReadOnlyList<OrderTrackingStep> Steps);
