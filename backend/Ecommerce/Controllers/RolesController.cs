using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Roles;

namespace Ecommerce.Controllers;

[HasPermission(PermissionKeys.RolesManage)]
[Route("api/Admin/[controller]")]
[ApiController]
public class RolesController(IRoleService roleService) : ControllerBase
{
    private readonly IRoleService _roleService = roleService;

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _roleService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<RoleResponse>>(StatusCodes.Status200OK, "Roles loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _roleService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Role not found."));

        return Ok(new ApiResponse<RoleResponse>(StatusCodes.Status200OK, "Role loaded.", result.Value));
    }

    [HttpGet("~/api/Admin/Permissions")]
    public async Task<IActionResult> GetPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        var result = await _roleService.GetPermissionCatalogAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<PermissionResponse>>(StatusCodes.Status200OK, "Permissions loaded.", result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create role."));

        var response = new ApiResponse<RoleResponse>(StatusCodes.Status201Created, "Role created.", result.Value);
        return Created($"/api/Admin/Roles/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update role."));

        return Ok(new ApiResponse<RoleResponse>(StatusCodes.Status200OK, "Role updated.", result.Value));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _roleService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete role."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Role deleted."));
    }
}
