using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Products;

namespace Ecommerce.Controllers
{
    // Storefront-facing, unauthenticated, read-only.
    // Every write action now lives on AdminProductsController behind
    // AdminBearer + products.manage.
    [Route("api/[controller]")]
    [ApiController]
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

        [HttpGet("{slug}")]
        public async Task<IActionResult> Get([FromRoute] string slug, CancellationToken cancellationToken)
        {
            var result = await _productService.GetByIdOrSlugAsync(slug, cancellationToken);

            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<ProductDetailsResponse>(StatusCodes.Status200OK, "Product retrieved successfully.", result.Value);
            return Ok(response);
        }
    }
}
