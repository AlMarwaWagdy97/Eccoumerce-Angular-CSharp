using Ecommerce.Contracts.Admins;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Services;

public class AdminService(ApplicationDbContext context, IAdminAuthService adminAuthService) : IAdminService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IAdminAuthService _adminAuthService = adminAuthService;

    public async Task<Result<IEnumerable<AdminResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var admins = await _context.Admins.Include(x => x.AdminRole).AsNoTracking().ToListAsync(cancellationToken);
        return Result.Success<IEnumerable<AdminResponse>>(admins.Select(MapAdmin).ToList());
    }

    public async Task<Result<AdminResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.Include(x => x.AdminRole).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return admin is null ? Result.Failure<AdminResponse>(AdminErrors.AdminNotFound) : Result.Success(MapAdmin(admin));
    }

    public async Task<Result<AdminResponse>> CreateAsync(CreateAdminRequest request, CancellationToken cancellationToken = default)
    {
        if (await _context.Admins.AnyAsync(x => x.Email == request.Email, cancellationToken))
            return Result.Failure<AdminResponse>(AdminErrors.EmailAlreadyExists);

        var role = await _context.AdminRoles.FirstOrDefaultAsync(x => x.Id == request.RoleId, cancellationToken);
        if (role is null)
            return Result.Failure<AdminResponse>(AdminErrors.RoleNotFound);

        var admin = new Admin
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            AdminRoleId = role.Id,
            AdminRole = role,
            IsActive = true,
        };
        // Unusable placeholder — nobody, including the creating admin, ever knows this
        // value. The new admin sets a real password themselves via the emailed link.
        admin.PasswordHash = new PasswordHasher<Admin>().HashPassword(admin, Guid.NewGuid().ToString());

        _context.Admins.Add(admin);
        await _context.SaveChangesAsync(cancellationToken);

        await _adminAuthService.ForgotPasswordAsync(admin.Email, cancellationToken);

        return Result.Success(MapAdmin(admin));
    }

    public async Task<Result<AdminResponse>> UpdateAsync(long id, UpdateAdminRequest request, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.Include(x => x.AdminRole).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (admin is null)
            return Result.Failure<AdminResponse>(AdminErrors.AdminNotFound);

        var role = await _context.AdminRoles.FirstOrDefaultAsync(x => x.Id == request.RoleId, cancellationToken);
        if (role is null)
            return Result.Failure<AdminResponse>(AdminErrors.RoleNotFound);

        admin.FirstName = request.FirstName;
        admin.LastName = request.LastName;
        admin.PhoneNumber = request.PhoneNumber;
        admin.AdminRoleId = role.Id;
        admin.AdminRole = role;
        admin.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(MapAdmin(admin));
    }

    public async Task<Result> SetStatusAsync(long id, bool isActive, long currentAdminId, CancellationToken cancellationToken = default)
    {
        if (id == currentAdminId)
            return Result.Failure(AdminErrors.CannotModifyOwnAccount);

        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (admin is null)
            return Result.Failure(AdminErrors.AdminNotFound);

        admin.IsActive = isActive;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long currentAdminId, CancellationToken cancellationToken = default)
    {
        if (id == currentAdminId)
            return Result.Failure(AdminErrors.CannotModifyOwnAccount);

        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (admin is null)
            return Result.Failure(AdminErrors.AdminNotFound);

        _context.Admins.Remove(admin);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static AdminResponse MapAdmin(Admin admin) => new(
        admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.PhoneNumber,
        admin.AdminRoleId, admin.AdminRole.Name, admin.IsActive, admin.CreatedOn);
}
