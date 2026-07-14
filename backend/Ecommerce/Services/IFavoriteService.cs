using Ecommerce.Contracts.Favorites;

namespace Ecommerce.Services;

public interface IFavoriteService
{
    Task<Result<IEnumerable<FavoriteResponse>>> GetAllAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result> AddAsync(string userId, long productId, CancellationToken cancellationToken = default);
    Task<Result> RemoveAsync(string userId, long productId, CancellationToken cancellationToken = default);
}
