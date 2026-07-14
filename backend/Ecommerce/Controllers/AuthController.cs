using Ecommerce.Abstractions;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Authentication;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    IAuthService authService,
    IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _authService.RegisterAsync(request, cancellationToken);
        if (!authResult.IsSuccess)
        {
            var errorResponse = new ApiResponse<object>(StatusCodes.Status400BadRequest, authResult.Error.Description ?? "Registration failed.");
            return BadRequest(errorResponse);
        }

        var response = new ApiResponse<object>(StatusCodes.Status200OK, "Register Sucessed.", authResult.Value);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _authService.GetTokenAsync(request.Email, request.Password, cancellationToken);

        if (!authResult.IsSuccess)
        {
            var errorResponse = new ApiResponse<object>(StatusCodes.Status400BadRequest, authResult.Error.Description ?? "Login failed.");
            return BadRequest(errorResponse);
        }

        var response = new ApiResponse<object>(StatusCodes.Status200OK, "Login Sucessed.", authResult.Value);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _authService.GetRefreshTokenAsync(request.Token, request.RefreshToken, cancellationToken);

        return authResult.IsSuccess ? Ok(authResult.Value) : authResult.ToProblem(StatusCodes.Status400BadRequest);
    }

    [HttpPost("revoke-refresh-token")]
    public async Task<IActionResult> RevokeRefreshTokenAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RevokeRefreshTokenAsync(request.Token, request.RefreshToken, cancellationToken);

        return result.IsSuccess ? Ok() : result.ToProblem(StatusCodes.Status400BadRequest);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _authService.LogoutAsync(userId, cancellationToken);

        return result.IsSuccess
            ? Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Logged out."))
            : result.ToProblem(StatusCodes.Status400BadRequest);
    }

    private string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
