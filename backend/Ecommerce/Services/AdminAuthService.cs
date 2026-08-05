using Ecommerce.Authentication;
using Ecommerce.Contracts.AdminAuth;
using Ecommerce.Email;
using Ecommerce.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Services;

public class AdminAuthService(
    ApplicationDbContext context,
    IAdminJwtProvider jwtProvider,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions) : IAdminAuthService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IAdminJwtProvider _jwtProvider = jwtProvider;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;
    private readonly PasswordHasher<Admin> _passwordHasher = new();
    private readonly int _refreshTokenExpiryDays = 14;
    private readonly int _resetTokenExpiryMinutes = 60;

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

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (admin is null)
            return Result.Success(); // no enumeration signal

        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        admin.PasswordResetTokens.Add(new AdminPasswordResetToken
        {
            Token = token,
            ExpiresOn = DateTime.UtcNow.AddMinutes(_resetTokenExpiryMinutes)
        });
        await _context.SaveChangesAsync(cancellationToken);

        var resetLink = $"{_frontendOptions.AdminAppUrl}/auth/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        await _emailSender.SendAsync(
            email,
            "Reset your ShopDemo admin password",
            $"<p>Click the link below to set a new password. This link expires in {_resetTokenExpiryMinutes} minutes.</p><p><a href=\"{resetLink}\">{resetLink}</a></p>",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        var resetToken = admin?.PasswordResetTokens.SingleOrDefault(x => x.Token == token && x.IsUsable);

        if (admin is null || resetToken is null)
            return Result.Failure(AdminAuthErrors.InvalidResetToken);

        admin.PasswordHash = _passwordHasher.HashPassword(admin, newPassword);
        resetToken.UsedOn = DateTime.UtcNow;

        foreach (var refreshToken in admin.RefreshTokens.Where(x => x.IsActive))
            refreshToken.RevokedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
