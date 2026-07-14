using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Cards;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class CardsController(ICardService cardService, IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    private readonly ICardService _cardService = cardService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _cardService.GetAllAsync(userId, cancellationToken);
        return Ok(new ApiResponse<IEnumerable<CardResponse>>(StatusCodes.Status200OK, "Cards loaded.", result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> AddAsync([FromBody] CardRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _cardService.AddAsync(userId, request, cancellationToken);
        var response = new ApiResponse<CardResponse>(StatusCodes.Status201Created, "Card added.", result.Value);
        return Created($"/api/Cards/{result.Value.Id}", response);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _cardService.DeleteAsync(userId, id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Card not found."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Card deleted."));
    }

    [HttpPut("{id:long}/default")]
    public async Task<IActionResult> SetDefaultAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _cardService.SetDefaultAsync(userId, id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Card not found."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Default card updated."));
    }

    private string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
