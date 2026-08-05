using Ecommerce.Contracts.Roles;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Services;

public class RoleService(ApplicationDbContext context) : IRoleService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Result<IEnumerable<RoleResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _context.AdminRoles.Include(x => x.Permissions).AsNoTracking().ToListAsync(cancellationToken);
        return Result.Success<IEnumerable<RoleResponse>>(roles.Select(MapRole).ToList());
    }

    public async Task<Result<RoleResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var role = await _context.AdminRoles.Include(x => x.Permissions).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return role is null ? Result.Failure<RoleResponse>(RoleErrors.RoleNotFound) : Result.Success(MapRole(role));
    }

    public async Task<Result<IEnumerable<PermissionResponse>>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await _context.Permissions.AsNoTracking().OrderBy(x => x.Module).ThenBy(x => x.Key).ToListAsync(cancellationToken);
        return Result.Success<IEnumerable<PermissionResponse>>(permissions.Select(MapPermission).ToList());
    }

    public async Task<Result<RoleResponse>> CreateAsync(RoleRequest request, CancellationToken cancellationToken = default)
    {
        if (await _context.AdminRoles.AnyAsync(x => x.Name == request.Name, cancellationToken))
            return Result.Failure<RoleResponse>(RoleErrors.RoleNameExists);

        var permissionsResult = await ResolvePermissionsAsync(request.PermissionKeys, cancellationToken);
        if (!permissionsResult.IsSuccess)
            return Result.Failure<RoleResponse>(permissionsResult.Error);

        var role = new AdminRole { Name = request.Name, Description = request.Description, Permissions = permissionsResult.Value };
        _context.AdminRoles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapRole(role));
    }

    public async Task<Result<RoleResponse>> UpdateAsync(long id, RoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _context.AdminRoles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
            return Result.Failure<RoleResponse>(RoleErrors.RoleNotFound);

        if (role.IsSystem)
            return Result.Failure<RoleResponse>(RoleErrors.SystemRoleProtected);

        if (await _context.AdminRoles.AnyAsync(x => x.Id != id && x.Name == request.Name, cancellationToken))
            return Result.Failure<RoleResponse>(RoleErrors.RoleNameExists);

        var permissionsResult = await ResolvePermissionsAsync(request.PermissionKeys, cancellationToken);
        if (!permissionsResult.IsSuccess)
            return Result.Failure<RoleResponse>(permissionsResult.Error);

        role.Name = request.Name;
        role.Description = request.Description;
        role.Permissions = permissionsResult.Value;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapRole(role));
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var role = await _context.AdminRoles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
            return Result.Failure(RoleErrors.RoleNotFound);

        if (role.IsSystem)
            return Result.Failure(RoleErrors.SystemRoleProtected);

        if (await _context.Admins.AnyAsync(x => x.AdminRoleId == id, cancellationToken))
            return Result.Failure(RoleErrors.RoleInUse);

        _context.AdminRoles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<List<Permission>>> ResolvePermissionsAsync(List<string> keys, CancellationToken cancellationToken)
    {
        var distinctKeys = keys.Distinct().ToList();
        var permissions = await _context.Permissions.Where(x => distinctKeys.Contains(x.Key)).ToListAsync(cancellationToken);

        return permissions.Count != distinctKeys.Count
            ? Result.Failure<List<Permission>>(RoleErrors.UnknownPermissionKey)
            : Result.Success(permissions);
    }

    private static RoleResponse MapRole(AdminRole role) => new(
        role.Id, role.Name, role.Description, role.IsSystem, role.Permissions.Select(MapPermission).ToList());

    private static PermissionResponse MapPermission(Permission permission) => new(
        permission.Id, permission.Key, permission.Module, permission.Description);
}
