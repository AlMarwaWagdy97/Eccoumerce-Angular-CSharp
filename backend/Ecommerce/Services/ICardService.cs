using Ecommerce.Contracts.Cards;

namespace Ecommerce.Services;

public interface ICardService
{
    Task<Result<IEnumerable<CardResponse>>> GetAllAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<CardResponse>> AddAsync(string userId, CardRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string userId, long id, CancellationToken cancellationToken = default);
    Task<Result> SetDefaultAsync(string userId, long id, CancellationToken cancellationToken = default);
}
