using Ecommerce.Contracts.Products;

namespace Ecommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController(IProductService productService) : ControllerBase
    {
        private readonly IProductService _productService = productService;

        [HttpGet("")]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var products = await _productService.GetAllAsync(cancellationToken);
            return Ok(products.Adapt<IEnumerable<ProductResponse>>());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _productService.GetAsync(id, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : result.ToProblem(StatusCodes.Status404NotFound);
        }

        [HttpPost("")]
        public async Task<IActionResult> Add([FromBody] ProductRequest request, CancellationToken cancellationToken)
        {
            var result = await _productService.AddAsync(request, cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
                : result.ToProblem(StatusCodes.Status409Conflict);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] ProductRequest request, CancellationToken cancellationToken)
        {
            var result = await _productService.UpdateAsync(id, request, cancellationToken);
            return result.IsSuccess ? NoContent() : result.ToProblem(StatusCodes.Status404NotFound);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _productService.DeleteAsync(id, cancellationToken);
            return result.IsSuccess ? NoContent() : result.ToProblem(StatusCodes.Status404NotFound);
        }

        [HttpPut("{id}/toggleStatus")]
        public async Task<IActionResult> ToggleStatus([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _productService.ToggleStatusAsync(id, cancellationToken);
            return result.IsSuccess ? NoContent() : result.ToProblem(StatusCodes.Status404NotFound);
        }
    }
}
