using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Addresses;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class AddressesController(IAddressService addressService, IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    private readonly IAddressService _addressService = addressService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _addressService.GetAllAsync(userId, cancellationToken);
        return Ok(new ApiResponse<IEnumerable<AddressResponse>>(StatusCodes.Status200OK, "Addresses loaded.", result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> AddAsync([FromBody] AddressRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _addressService.AddAsync(userId, request, cancellationToken);
        var response = new ApiResponse<AddressResponse>(StatusCodes.Status201Created, "Address added.", result.Value);
        return Created($"/api/Addresses/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromBody] AddressRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _addressService.UpdateAsync(userId, id, request, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Address not found."));

        return Ok(new ApiResponse<AddressResponse>(StatusCodes.Status200OK, "Address updated.", result.Value));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _addressService.DeleteAsync(userId, id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Address not found."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Address deleted."));
    }

    [HttpPut("{id:long}/default")]
    public async Task<IActionResult> SetDefaultAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _addressService.SetDefaultAsync(userId, id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Address not found."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Default address updated."));
    }

    private string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
