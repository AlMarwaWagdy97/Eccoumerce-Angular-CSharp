namespace Ecommerce.Contracts.Clients;

public record ClientDetailResponse(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool EmailConfirmed,
    int OrderCount,
    double LifetimeTotal);
