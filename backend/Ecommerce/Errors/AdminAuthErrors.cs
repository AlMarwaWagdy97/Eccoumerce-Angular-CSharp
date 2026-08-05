namespace Ecommerce.Errors;

public static class AdminAuthErrors
{
    public static readonly Error InvalidCredentials = new("AdminAuth.InvalidCredentials", "Invalid email or password");
    public static readonly Error AccountInactive = new("AdminAuth.AccountInactive", "This admin account has been deactivated");
    public static readonly Error InvalidJwtToken = new("AdminAuth.InvalidJwtToken", "Invalid JWT token");
    public static readonly Error InvalidRefreshToken = new("AdminAuth.InvalidRefreshToken", "Invalid refresh token");
    public static readonly Error InvalidResetToken = new("AdminAuth.InvalidResetToken", "This password reset link is invalid or has expired");
}
