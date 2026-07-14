namespace Ecommerce.Contracts.Orders;

public record CreateOrderItemRequest(long ProductId, int Quantity);

public record CreateOrderRequest(long AddressId, IReadOnlyList<CreateOrderItemRequest> Items);
