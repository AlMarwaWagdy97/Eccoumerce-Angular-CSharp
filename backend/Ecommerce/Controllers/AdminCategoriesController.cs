using Ecommerce.Authorization;
using Ecommerce.Contracts.Categories;
using Ecommerce.Contracts.Common;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Categories")]
[ApiController]
public class AdminCategoriesController(ICategoryService categoryService) : ControllerBase
{
    private readonly ICategoryService _categoryService = categoryService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.CategoriesView)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(cancellationToken);
        var response = new ApiResponse<IEnumerable<CategoryResponse>>(
            StatusCodes.Status200OK, "Categories loaded.", categories.Adapt<IEnumerable<CategoryResponse>>());

        return Ok(response);
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionKeys.CategoriesView)]
    public async Task<IActionResult> GetAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Category not found."));

        return Ok(new ApiResponse<CategoryResponse>(StatusCodes.Status200OK, "Category loaded.", result.Value));
    }

    [HttpPost("")]
    [HasPermission(PermissionKeys.CategoriesManage)]
    public async Task<IActionResult> AddAsync([FromForm] CategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.AddAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create category."));

        var response = new ApiResponse<CategoryResponse>(StatusCodes.Status201Created, "Category created.", result.Value);
        return Created($"/api/Admin/Categories/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionKeys.CategoriesManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromForm] CategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update category."));

        return Ok(new ApiResponse<CategoryResponse>(StatusCodes.Status200OK, "Category updated.", result.Value));
    }

    [HttpPut("{id:long}/toggleStatus")]
    [HasPermission(PermissionKeys.CategoriesManage)]
    public async Task<IActionResult> ToggleStatusAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.ToggleStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not toggle category status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Category status toggled."));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionKeys.CategoriesManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete category."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Category deleted."));
    }
}
