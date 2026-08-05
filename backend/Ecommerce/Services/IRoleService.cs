using Ecommerce.Contracts.Roles;

namespace Ecommerce.Services;

public interface IRoleService
{
    Task<Result<IEnumerable<RoleResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<RoleResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<PermissionResponse>>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default);
    Task<Result<RoleResponse>> CreateAsync(RoleRequest request, CancellationToken cancellationToken = default);
    Task<Result<RoleResponse>> UpdateAsync(long id, RoleRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
