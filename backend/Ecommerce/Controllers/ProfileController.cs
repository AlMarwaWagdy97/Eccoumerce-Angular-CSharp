using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Profile;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class ProfileController(IProfileService profileService, IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    private readonly IProfileService _profileService = profileService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _profileService.GetAsync(userId, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Profile not found."));

        return Ok(new ApiResponse<ProfileResponse>(StatusCodes.Status200OK, "Profile loaded.", result.Value));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAsync([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _profileService.UpdateAsync(userId, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Profile update failed."));

        return Ok(new ApiResponse<ProfileResponse>(StatusCodes.Status200OK, "Profile updated.", result.Value));
    }

    private string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
