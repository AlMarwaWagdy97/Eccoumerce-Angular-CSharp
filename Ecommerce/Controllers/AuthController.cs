using Ecommerce.Abstractions;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Products;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ecommerce.Controllers;

[Route("[controller]")]
[ApiController]
public class AuthController(IAuthService authService) : ControllerBase
{

    private readonly IAuthService _authService = authService;

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromForm] RegisterRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _authService.RegisterAsync(request, cancellationToken);
        if (!authResult.IsSuccess)
        {
            var errorResponse = new ApiResponse<object>(StatusCodes.Status400BadRequest,"");
            return NotFound(errorResponse);
        }
        var response = new ApiResponse<object>(StatusCodes.Status200OK, "Register Sucessed.", authResult.Value);
        return Ok(response);
    }

    [HttpPost("Login")]
    public async Task<IActionResult> LoginAsync([FromForm] LoginRequest request, CancellationToken cancellationToken)
    {
        var authResult = await _authService.GetTokenAsync(request.Email, request.Password, cancellationToken);

        if (!authResult.IsSuccess)
        {
            var errorResponse = new ApiResponse<object>(StatusCodes.Status400BadRequest, "");
            return NotFound(errorResponse);
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
}