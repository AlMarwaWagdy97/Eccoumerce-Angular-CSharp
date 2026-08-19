using Ecommerce.Contracts.Categories;
using Ecommerce.Contracts.Common;

namespace Ecommerce.Controllers
{
    // Storefront-facing, unauthenticated, read-only.
    // Every write action now lives on AdminCategoriesController behind
    // AdminBearer + categories.manage.
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController(ICategoryService categoryService) : ControllerBase
    {
        private readonly ICategoryService _categoryService = categoryService;

        [HttpGet("")]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var categories = await _categoryService.GetAllAsync(cancellationToken);
            var response = new ApiResponse<IEnumerable<CategoryResponse>>(
                StatusCodes.Status200OK, "", categories.Adapt<IEnumerable<CategoryResponse>>());

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetAsync(id, cancellationToken);
            if (!result.IsSuccess)
                return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Category not found."));

            return Ok(new ApiResponse<CategoryResponse>(StatusCodes.Status200OK, "Category retrieved successfully.", result.Value));
        }
    }
}
