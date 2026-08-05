// backend/Ecommerce/Controllers/AdminAuthController.cs
using Ecommerce.Contracts.AdminAuth;
using Ecommerce.Contracts.Common;
using System.Security.Claims;

namespace Ecommerce.Controllers;

// Explicit literal route, not "api/Admin/[controller]" — the [controller] token
// would resolve to "AdminAuth" (class name minus "Controller"), not "Auth".
[Route("api/Admin/Auth")]
[ApiController]
public class AdminAuthController(IAdminAuthService adminAuthService) : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService = adminAuthService;

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminAuthService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Login failed."));

        return Ok(new ApiResponse<AdminAuthResponse>(StatusCodes.Status200OK, "Login successful.", result.Value));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync([FromBody] AdminRefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminAuthService.RefreshTokenAsync(request.Token, request.RefreshToken, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Refresh failed."));

        return Ok(new ApiResponse<AdminAuthResponse>(StatusCodes.Status200OK, "Token refreshed.", result.Value));
    }

    [Authorize(AuthenticationSchemes = Ecommerce.Authorization.AdminAuthDefaults.Scheme)]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminId is null || !long.TryParse(adminId, out var id))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        await _adminAuthService.LogoutAsync(id, cancellationToken);
        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Logged out."));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] AdminForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _adminAuthService.ForgotPasswordAsync(request.Email, cancellationToken);
        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "If that email is registered, a reset link has been sent."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] AdminResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminAuthService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Reset failed."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Password updated. You can now log in."));
    }
}
