using Ecommerce.Abstractions;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(
    IAuthService authService,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext context,
    IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly ApplicationDbContext _context = context;
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

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, "User not found."));

        var response = new ApiResponse<ProfileResponse>(StatusCodes.Status200OK, "Profile loaded.", new ProfileResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Id != null ? DateTime.UtcNow : DateTime.UtcNow));

        return Ok(response);
    }

    [Authorize]
    [HttpGet("orders")]
    public async Task<IActionResult> Orders(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedOn)
            .Select(x => new OrderSummaryResponse(
                x.Id,
                x.OrderNumber,
                x.Status.ToString(),
                x.Total,
                x.CreatedOn,
                x.OrderNumber))
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<IEnumerable<OrderSummaryResponse>>(StatusCodes.Status200OK, "Orders loaded.", orders));
    }

    [Authorize]
    [HttpGet("orders/{orderNumber}/tracking")]
    public async Task<IActionResult> Tracking(string orderNumber, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, "Order not found."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Tracking loaded.", new { order.OrderNumber, order.Status, order.CreatedOn }));
    }

    [Authorize]
    [HttpGet("favorites")]
    public async Task<IActionResult> Favorites(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var favorites = await _context.Favorites
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new FavoriteResponse(
                x.Id,
                x.ProductId,
                x.Product.Title,
                x.Product.Image,
                x.Product.PriceAfterSale ?? x.Product.Price,
                x.Product.Slug))
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<IEnumerable<FavoriteResponse>>(StatusCodes.Status200OK, "Favorites loaded.", favorites));
    }

    [Authorize]
    [HttpPost("favorites/{productId:long}")]
    public async Task<IActionResult> AddFavorite([FromRoute] long productId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var exists = await _context.Favorites.AnyAsync(x => x.UserId == userId && x.ProductId == productId, cancellationToken);
        if (exists)
            return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Already in favorites."));

        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product is null)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, "Product not found."));

        _context.Favorites.Add(new Favorite { UserId = userId, ProductId = productId });
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Added to favorites."));
    }

    [Authorize]
    [HttpDelete("favorites/{productId:long}")]
    public async Task<IActionResult> RemoveFavorite([FromRoute] long productId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var favorite = await _context.Favorites.FirstOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId, cancellationToken);
        if (favorite is null)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, "Favorite not found."));

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Removed from favorites."));
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

    private string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
