using Ecommerce.Contracts.Addresses;

namespace Ecommerce.Services;

public interface IAddressService
{
    Task<Result<IEnumerable<AddressResponse>>> GetAllAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<AddressResponse>> AddAsync(string userId, AddressRequest request, CancellationToken cancellationToken = default);
    Task<Result<AddressResponse>> UpdateAsync(string userId, long id, AddressRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string userId, long id, CancellationToken cancellationToken = default);
    Task<Result> SetDefaultAsync(string userId, long id, CancellationToken cancellationToken = default);
}
