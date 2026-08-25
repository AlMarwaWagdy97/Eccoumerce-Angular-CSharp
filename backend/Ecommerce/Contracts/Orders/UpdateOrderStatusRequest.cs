using System.Text.Json.Serialization;

namespace Ecommerce.Contracts.Orders;

// OrderStatus/PaymentStatus are plain int-backed enums with no global
// JsonStringEnumConverter registered anywhere in this API — every existing
// read endpoint converts to string manually via .ToString() instead, and
// nothing before this request has ever taken an enum as client input.
// Without this attribute the request body would have to send raw numbers
// (0-4 / 0-2) instead of the same status names the admin UI already shows.
public record UpdateOrderStatusRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] OrderStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] PaymentStatus PaymentStatus);
