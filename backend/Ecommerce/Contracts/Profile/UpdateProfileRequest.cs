namespace Ecommerce.Contracts.Profile;

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? PhoneNumber);
