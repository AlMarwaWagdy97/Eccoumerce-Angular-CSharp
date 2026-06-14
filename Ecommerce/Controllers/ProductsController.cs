using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Products;

namespace Ecommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductsController(IProductService productService) : ControllerBase
    {
        private readonly IProductService _productService = productService;

        [HttpGet("")]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var products = await _productService.GetAllAsync(cancellationToken);
            var response = new ApiResponse<IEnumerable<ProductResponse>>(StatusCodes.Status200OK, "", products.Adapt<IEnumerable<ProductResponse>>());
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _productService.GetAsync(id, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<ProductResponse>(StatusCodes.Status200OK, "Product retrieved successfully.", result.Value);
            return Ok(response);
        }

        [HttpPost("")]
        public async Task<IActionResult> Add([FromForm] ProductRequest request, CancellationToken cancellationToken)
        {
            var result = await _productService.AddAsync(request, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status409Conflict, result.Error.Description ?? "Product conflict occurred.");
                return StatusCode(StatusCodes.Status409Conflict, errorResponse);
            }

            var response = new ApiResponse<ProductResponse>(StatusCodes.Status201Created, "Product created successfully.", result.Value);
            return CreatedAtAction(nameof(Get), new { id = result.Value.Id }, response);
        }
        
        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromForm] ProductRequest request, CancellationToken cancellationToken)
        {
            var result = await _productService.UpdateAsync(id, request, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<ProductResponse>(StatusCodes.Status200OK, "Product updated successfully.");
            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _productService.DeleteAsync(id, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<object>(StatusCodes.Status200OK, "Product deleted successfully.");
            return Ok(response);
        }

        [HttpPut("{id}/toggleStatus")]
        public async Task<IActionResult> ToggleStatus([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _productService.ToggleStatusAsync(id, cancellationToken);
            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<object>(StatusCodes.Status200OK, "Product status toggled successfully.");
            return Ok(response);
        }
    }
}
