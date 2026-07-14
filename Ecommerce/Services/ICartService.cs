using Ecommerce.Contracts.Cart;

namespace Ecommerce.Services
{
    public interface ICartService
    {
        Task<Result<CartResponse>> GetAsync(CancellationToken cancellationToken = default);
        Task<Result<CartCountResponse>> GetCountAsync(CancellationToken cancellationToken = default);
        Task<Result<CartResponse>> AddItemAsync(AddToCartRequest request, CancellationToken cancellationToken = default);
        Task<Result<CartResponse>> UpdateItemAsync(long productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);
        Task<Result> RemoveItemAsync(long productId, CancellationToken cancellationToken = default);
        Task<Result> ClearAsync(CancellationToken cancellationToken = default);
    }
}
