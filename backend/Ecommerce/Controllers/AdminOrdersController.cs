using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Orders;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Orders")]
[ApiController]
public class AdminOrdersController(IOrderAdminService orderAdminService) : ControllerBase
{
    private readonly IOrderAdminService _orderAdminService = orderAdminService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.OrdersView)]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? search,
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _orderAdminService.GetAllAsync(search, status, page, pageSize, cancellationToken);
        return Ok(new ApiResponse<OrdersPageResponse>(StatusCodes.Status200OK, "Orders loaded.", result.Value));
    }

    [HttpGet("{orderNumber}")]
    [HasPermission(PermissionKeys.OrdersView)]
    public async Task<IActionResult> GetByOrderNumberAsync([FromRoute] string orderNumber, CancellationToken cancellationToken)
    {
        var result = await _orderAdminService.GetByOrderNumberAsync(orderNumber, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Order not found."));

        return Ok(new ApiResponse<AdminOrderDetailResponse>(StatusCodes.Status200OK, "Order loaded.", result.Value));
    }

    [HttpPut("{orderNumber}/status")]
    [HasPermission(PermissionKeys.OrdersManage)]
    public async Task<IActionResult> UpdateStatusAsync([FromRoute] string orderNumber, [FromBody] UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderAdminService.UpdateStatusAsync(orderNumber, request.Status, request.PaymentStatus, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update order status."));

        return Ok(new ApiResponse<AdminOrderDetailResponse>(StatusCodes.Status200OK, "Order status updated.", result.Value));
    }
}
