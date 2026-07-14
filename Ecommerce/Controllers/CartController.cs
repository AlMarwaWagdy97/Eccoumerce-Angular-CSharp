using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Cart;

namespace Ecommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController(ICartService cartService) : ControllerBase
    {
        private readonly ICartService _cartService = cartService;

        [HttpGet("")]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var result = await _cartService.GetAsync(cancellationToken);
            if (!result.IsSuccess)
                return MapFailure(result.Error);

            var response = new ApiResponse<CartResponse>(StatusCodes.Status200OK, "Cart retrieved successfully.", result.Value);
            return Ok(response);
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetCount(CancellationToken cancellationToken)
        {
            var result = await _cartService.GetCountAsync(cancellationToken);
            if (!result.IsSuccess)
                return MapFailure(result.Error);

            var response = new ApiResponse<CartCountResponse>(StatusCodes.Status200OK, "", result.Value);
            return Ok(response);
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddItem([FromBody] AddToCartRequest request, CancellationToken cancellationToken)
        {
            var result = await _cartService.AddItemAsync(request, cancellationToken);
            if (!result.IsSuccess)
                return MapFailure(result.Error);

            var response = new ApiResponse<CartResponse>(StatusCodes.Status200OK, "Item added to cart successfully.", result.Value);
            return Ok(response);
        }

        [HttpPut("items/{productId}")]
        public async Task<IActionResult> UpdateItem([FromRoute] long productId, [FromBody] UpdateCartItemRequest request, CancellationToken cancellationToken)
        {
            var result = await _cartService.UpdateItemAsync(productId, request, cancellationToken);
            if (!result.IsSuccess)
                return MapFailure(result.Error);

            var response = new ApiResponse<CartResponse>(StatusCodes.Status200OK, "Cart item updated successfully.", result.Value);
            return Ok(response);
        }

        [HttpDelete("items/{productId}")]
        public async Task<IActionResult> RemoveItem([FromRoute] long productId, CancellationToken cancellationToken)
        {
            var result = await _cartService.RemoveItemAsync(productId, cancellationToken);
            if (!result.IsSuccess)
                return MapFailure(result.Error);

            var response = new ApiResponse<object>(StatusCodes.Status200OK, "Item removed from cart successfully.");
            return Ok(response);
        }

        [HttpDelete("")]
        public async Task<IActionResult> Clear(CancellationToken cancellationToken)
        {
            var result = await _cartService.ClearAsync(cancellationToken);
            if (!result.IsSuccess)
                return MapFailure(result.Error);

            var response = new ApiResponse<object>(StatusCodes.Status200OK, "Cart cleared successfully.");
            return Ok(response);
        }

        private IActionResult MapFailure(Error error)
        {
            var statusCode = error.Code switch
            {
                "Cart.UserNotAuthenticated" => StatusCodes.Status401Unauthorized,
                "Cart.ItemNotFound" or "Product.NotFound" => StatusCodes.Status404NotFound,
                "Cart.InvalidQuantity" or "Cart.InsufficientStock" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            };

            var errorResponse = new ApiResponse<object>(statusCode, error.Description ?? "Cart operation failed.");
            return StatusCode(statusCode, errorResponse);
        }
    }
}
