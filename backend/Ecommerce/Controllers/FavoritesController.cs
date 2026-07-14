using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Favorites;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class FavoritesController(IFavoriteService favoriteService, IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    private readonly IFavoriteService _favoriteService = favoriteService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _favoriteService.GetAllAsync(userId, cancellationToken);
        return Ok(new ApiResponse<IEnumerable<FavoriteResponse>>(StatusCodes.Status200OK, "Favorites loaded.", result.Value));
    }

    [HttpPost("{productId:long}")]
    public async Task<IActionResult> AddAsync([FromRoute] long productId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _favoriteService.AddAsync(userId, productId, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Added to favorites."));
    }

    [HttpDelete("{productId:long}")]
    public async Task<IActionResult> RemoveAsync([FromRoute] long productId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _favoriteService.RemoveAsync(userId, productId, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Favorite not found."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Removed from favorites."));
    }

    private string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
