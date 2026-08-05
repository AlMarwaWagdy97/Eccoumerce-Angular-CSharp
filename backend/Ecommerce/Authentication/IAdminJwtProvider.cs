namespace Ecommerce.Authentication;

public interface IAdminJwtProvider
{
    (string token, int expiresIn) GenerateToken(Admin admin, IEnumerable<string> permissions);
    string? ValidateToken(string token);
}
