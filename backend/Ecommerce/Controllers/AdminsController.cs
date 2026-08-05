using Ecommerce.Authorization;
using Ecommerce.Contracts.Admins;
using Ecommerce.Contracts.Common;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[HasPermission(PermissionKeys.AdminsManage)]
[Route("api/Admin/[controller]")]
[ApiController]
public class AdminsController(IAdminService adminService) : ControllerBase
{
    private readonly IAdminService _adminService = adminService;

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<AdminResponse>>(StatusCodes.Status200OK, "Admins loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _adminService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Admin not found."));

        return Ok(new ApiResponse<AdminResponse>(StatusCodes.Status200OK, "Admin loaded.", result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAdminRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create admin."));

        var response = new ApiResponse<AdminResponse>(StatusCodes.Status201Created, "Admin created. A set-password email has been sent.", result.Value);
        return Created($"/api/Admin/Admins/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromBody] UpdateAdminRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update admin."));

        return Ok(new ApiResponse<AdminResponse>(StatusCodes.Status200OK, "Admin updated.", result.Value));
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetStatusAsync([FromRoute] long id, [FromBody] SetAdminStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.SetStatusAsync(id, request.IsActive, GetCurrentAdminId(), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Admin status updated."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _adminService.DeleteAsync(id, GetCurrentAdminId(), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete admin."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Admin deleted."));
    }

    private long GetCurrentAdminId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
