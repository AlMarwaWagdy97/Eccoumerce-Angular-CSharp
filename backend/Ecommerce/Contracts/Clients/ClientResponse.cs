namespace Ecommerce.Contracts.Clients;

// IsActive is projected from Identity's lockout state — there is no IsActive column.
public record ClientResponse(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool EmailConfirmed);
