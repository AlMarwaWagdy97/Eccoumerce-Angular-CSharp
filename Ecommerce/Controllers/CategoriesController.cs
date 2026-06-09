using Ecommerce.Contracts.Categories;

namespace Ecommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController(ICategoryService categoryService) : ControllerBase
    {
        private readonly ICategoryService _categoryService = categoryService;

        [HttpGet("")]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var categories = await _categoryService.GetAllAsync(cancellationToken);
            return Ok(categories.Adapt<IEnumerable<CategoryResponse>>());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetAsync(id, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblem(StatusCodes.Status404NotFound);
        }

        [HttpPost("")]
        public async Task<IActionResult> Add([FromBody] CategoryRequest request, CancellationToken cancellationToken)
        {
            var result = await _categoryService.AddAsync(request, cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
                : result.ToProblem(StatusCodes.Status409Conflict);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] CategoryRequest request, CancellationToken cancellationToken)
        {
            var result = await _categoryService.UpdateAsync(id, request, cancellationToken);
            return result.IsSuccess ? NoContent() : result.ToProblem(StatusCodes.Status404NotFound);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.DeleteAsync(id, cancellationToken);
            return result.IsSuccess ? NoContent() : result.ToProblem(StatusCodes.Status404NotFound);
        }

        [HttpPut("{id}/toggleStatus")]
        public async Task<IActionResult> ToggleStatus([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.ToggleStatusAsync(id, cancellationToken);
            return result.IsSuccess ? NoContent() : result.ToProblem(StatusCodes.Status404NotFound);
        }
    }
}
