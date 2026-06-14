using Ecommerce.Contracts.Categories;
using Ecommerce.Contracts.Common;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Ecommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CategoriesController(ICategoryService categoryService) : ControllerBase
    {
        private readonly ICategoryService _categoryService = categoryService;

        [HttpGet("")]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var categories = await _categoryService.GetAllAsync(cancellationToken);
            var response = new ApiResponse<IEnumerable<CategoryResponse>>(StatusCodes.Status200OK, "", categories.Adapt<IEnumerable<CategoryResponse>>());
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetAsync(id, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Category not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<CategoryResponse>(StatusCodes.Status200OK, "Category retrieved successfully.", result.Value);
            return Ok(response);
        }

        [HttpPost("")]
        public async Task<IActionResult> Add([FromForm] CategoryRequest request, CancellationToken cancellationToken)
        {
            var result = await _categoryService.AddAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status409Conflict, result.Error.Description ?? "Category conflict occurred.");
                return StatusCode(StatusCodes.Status409Conflict, errorResponse);
            }

            var response = new ApiResponse<CategoryResponse>(StatusCodes.Status201Created, "Category created successfully.", result.Value);
            return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromForm] CategoryRequest request, CancellationToken cancellationToken)
        {
            var result = await _categoryService.UpdateAsync(id, request, cancellationToken);
           if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Category not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<CategoryResponse>(StatusCodes.Status200OK, "Category updated successfully.", result.Value);
            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.DeleteAsync(id, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Category not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<object>(StatusCodes.Status200OK, "Category deleted successfully.");
            return Ok(response);
        }

        [HttpPut("{id}/toggleStatus")]
        public async Task<IActionResult> ToggleStatus([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.ToggleStatusAsync(id, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Category not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<object>(StatusCodes.Status200OK, "Category status toggled successfully.");
            return Ok(response);
        }
    }
}
