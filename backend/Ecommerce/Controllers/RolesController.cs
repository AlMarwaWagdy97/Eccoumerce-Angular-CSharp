using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Roles;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/[controller]")]
[ApiController]
public class RolesController(IRoleService roleService) : ControllerBase
{
    private readonly IRoleService _roleService = roleService;

    // The Admins page needs the role list to populate its role picker, so any admin who can
    // manage admins (not just those who can manage roles) is allowed to list roles here.
    // Every other action below still requires roles.manage specifically.
    [HttpGet]
    [HasPermission(PermissionKeys.RolesManage, PermissionKeys.AdminsManage)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _roleService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<RoleResponse>>(StatusCodes.Status200OK, "Roles loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionKeys.RolesManage)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _roleService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Role not found."));

        return Ok(new ApiResponse<RoleResponse>(StatusCodes.Status200OK, "Role loaded.", result.Value));
    }

    [HttpGet("~/api/Admin/Permissions")]
    [HasPermission(PermissionKeys.RolesManage)]
    public async Task<IActionResult> GetPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        var result = await _roleService.GetPermissionCatalogAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<PermissionResponse>>(StatusCodes.Status200OK, "Permissions loaded.", result.Value));
    }

    [HttpPost]
    [HasPermission(PermissionKeys.RolesManage)]
    public async Task<IActionResult> CreateAsync([FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create role."));

        var response = new ApiResponse<RoleResponse>(StatusCodes.Status201Created, "Role created.", result.Value);
        return Created($"/api/Admin/Roles/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionKeys.RolesManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update role."));

        return Ok(new ApiResponse<RoleResponse>(StatusCodes.Status200OK, "Role updated.", result.Value));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionKeys.RolesManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _roleService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete role."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Role deleted."));
    }
}
