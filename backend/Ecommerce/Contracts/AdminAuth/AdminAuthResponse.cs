namespace Ecommerce.Contracts.AdminAuth;

public record AdminAuthResponse(
    long Id,
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    string[] Permissions,
    string Token,
    int ExpiresIn,
    string RefreshToken,
    DateTime RefreshTokenExpiration
);
