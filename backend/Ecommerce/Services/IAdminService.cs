using Ecommerce.Contracts.Admins;

namespace Ecommerce.Services;

public interface IAdminService
{
    Task<Result<IEnumerable<AdminResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<AdminResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<AdminResponse>> CreateAsync(CreateAdminRequest request, CancellationToken cancellationToken = default);
    Task<Result<AdminResponse>> UpdateAsync(long id, UpdateAdminRequest request, long currentAdminId, CancellationToken cancellationToken = default);
    Task<Result> SetStatusAsync(long id, bool isActive, long currentAdminId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(long id, long currentAdminId, CancellationToken cancellationToken = default);
}
