namespace Ecommerce.Contracts.Authentication;

public record ProfileResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    DateTime CreatedOn);
