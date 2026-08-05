using Ecommerce.Contracts.AdminAuth;

namespace Ecommerce.Services;

public interface IAdminAuthService
{
    Task<Result<AdminAuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AdminAuthResponse>> RefreshTokenAsync(string token, string refreshToken, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(long adminId, CancellationToken cancellationToken = default);
}
