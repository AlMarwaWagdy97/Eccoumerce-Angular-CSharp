namespace Ecommerce.Contracts.Admins;

public record AdminResponse(
    long Id, string FirstName, string LastName, string Email, string? PhoneNumber,
    long RoleId, string RoleName, bool IsActive, DateTime CreatedOn);
