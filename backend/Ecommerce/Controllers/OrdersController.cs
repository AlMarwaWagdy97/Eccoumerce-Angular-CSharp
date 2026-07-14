using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Orders;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class OrdersController(IOrderService orderService, IHttpContextAccessor httpContextAccessor) : ControllerBase
{
    private readonly IOrderService _orderService = orderService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _orderService.CreateAsync(userId, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Order could not be created."));

        var response = new ApiResponse<OrderResponse>(StatusCodes.Status201Created, "Order created.", result.Value);
        return Created($"/api/Orders/{Uri.EscapeDataString(result.Value.OrderNumber)}", response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _orderService.GetAllAsync(userId, cancellationToken);
        return Ok(new ApiResponse<IEnumerable<OrderSummaryResponse>>(StatusCodes.Status200OK, "Orders loaded.", result.Value));
    }

    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> GetAsync([FromRoute] string orderNumber, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _orderService.GetAsync(userId, orderNumber, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Order not found."));

        return Ok(new ApiResponse<OrderResponse>(StatusCodes.Status200OK, "Order loaded.", result.Value));
    }

    [HttpGet("{orderNumber}/tracking")]
    public async Task<IActionResult> GetTrackingAsync([FromRoute] string orderNumber, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        var result = await _orderService.GetTrackingAsync(userId, orderNumber, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Order not found."));

        return Ok(new ApiResponse<OrderTrackingResponse>(StatusCodes.Status200OK, "Tracking loaded.", result.Value));
    }

    private string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
