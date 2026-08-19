using Ecommerce.Authorization;
using Ecommerce.Contracts.Clients;
using Ecommerce.Contracts.Common;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Clients")]
[ApiController]
public class AdminClientsController(IClientService clientService) : ControllerBase
{
    private readonly IClientService _clientService = clientService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.ClientsView)]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _clientService.GetAllAsync(search, page, pageSize, cancellationToken);
        return Ok(new ApiResponse<ClientsPageResponse>(StatusCodes.Status200OK, "Clients loaded.", result.Value));
    }

    [HttpGet("{id}")]
    [HasPermission(PermissionKeys.ClientsView)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _clientService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Client not found."));

        return Ok(new ApiResponse<ClientDetailResponse>(StatusCodes.Status200OK, "Client loaded.", result.Value));
    }

    [HttpPut("{id}")]
    [HasPermission(PermissionKeys.ClientsManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] string id, [FromBody] UpdateClientRequest request, CancellationToken cancellationToken)
    {
        var result = await _clientService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update client."));

        return Ok(new ApiResponse<ClientResponse>(StatusCodes.Status200OK, "Client updated.", result.Value));
    }

    [HttpPut("{id}/toggleStatus")]
    [HasPermission(PermissionKeys.ClientsManage)]
    public async Task<IActionResult> ToggleStatusAsync([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _clientService.ToggleStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update client status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Client status updated."));
    }

    [HttpDelete("{id}")]
    [HasPermission(PermissionKeys.ClientsManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _clientService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete client."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Client deleted."));
    }
}
