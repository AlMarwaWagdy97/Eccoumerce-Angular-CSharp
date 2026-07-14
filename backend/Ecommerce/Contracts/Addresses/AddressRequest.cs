namespace Ecommerce.Contracts.Addresses;

public record AddressRequest(
    string FullName,
    string Phone,
    string Line1,
    string? Line2,
    string City,
    string State,
    string Country,
    string? PostalCode,
    bool IsDefault);
