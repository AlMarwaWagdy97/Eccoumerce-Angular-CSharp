using Ecommerce.Authentication;
using Ecommerce.Contracts.AdminAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Services;

public class AdminAuthService(ApplicationDbContext context, IAdminJwtProvider jwtProvider) : IAdminAuthService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IAdminJwtProvider _jwtProvider = jwtProvider;
    private readonly PasswordHasher<Admin> _passwordHasher = new();
    private readonly int _refreshTokenExpiryDays = 14;

    public async Task<Result<AdminAuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins
            .Include(x => x.AdminRole).ThenInclude(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (admin is null || _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.InvalidCredentials);

        if (!admin.IsActive)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.AccountInactive);

        return Result.Success(await IssueTokensAsync(admin, cancellationToken));
    }

    public async Task<Result<AdminAuthResponse>> RefreshTokenAsync(string token, string refreshToken, CancellationToken cancellationToken = default)
    {
        var adminId = _jwtProvider.ValidateToken(token);
        if (adminId is null || !long.TryParse(adminId, out var id))
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.InvalidJwtToken);

        var admin = await _context.Admins
            .Include(x => x.AdminRole).ThenInclude(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (admin is null)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.InvalidJwtToken);

        var activeToken = admin.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken && x.IsActive);
        if (activeToken is null)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.InvalidRefreshToken);

        if (!admin.IsActive)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.AccountInactive);

        activeToken.RevokedOn = DateTime.UtcNow;

        return Result.Success(await IssueTokensAsync(admin, cancellationToken));
    }

    public async Task<Result> LogoutAsync(long adminId, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Id == adminId, cancellationToken);
        if (admin is null)
            return Result.Failure(AdminAuthErrors.InvalidJwtToken);

        foreach (var refreshToken in admin.RefreshTokens.Where(x => x.IsActive))
            refreshToken.RevokedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<AdminAuthResponse> IssueTokensAsync(Admin admin, CancellationToken cancellationToken)
    {
        var permissions = admin.AdminRole.Permissions.Select(p => p.Key).ToArray();
        var (jwt, expiresIn) = _jwtProvider.GenerateToken(admin, permissions);

        var refreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        var refreshTokenExpiration = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays);

        admin.RefreshTokens.Add(new AdminRefreshToken { Token = refreshToken, ExpiresOn = refreshTokenExpiration });
        await _context.SaveChangesAsync(cancellationToken);

        return new AdminAuthResponse(
            admin.Id, admin.Email, admin.FirstName, admin.LastName, admin.AdminRole.Name, permissions,
            jwt, expiresIn, refreshToken, refreshTokenExpiration);
    }
}
