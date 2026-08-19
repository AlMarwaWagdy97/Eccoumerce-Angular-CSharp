using Ecommerce.Contracts.Clients;

namespace Ecommerce.Services;

public interface IClientService
{
    Task<Result<ClientsPageResponse>> GetAllAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<ClientDetailResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<ClientResponse>> UpdateAsync(string id, UpdateClientRequest request, CancellationToken cancellationToken = default);
    Task<Result> ToggleStatusAsync(string id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
