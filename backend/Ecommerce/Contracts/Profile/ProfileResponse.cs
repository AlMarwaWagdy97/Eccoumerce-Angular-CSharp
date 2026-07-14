namespace Ecommerce.Contracts.Profile;

public record ProfileResponse(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber);
