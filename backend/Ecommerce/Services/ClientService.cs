using Ecommerce.Contracts.Clients;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Services;

public class ClientService(ApplicationDbContext context, UserManager<ApplicationUser> userManager) : IClientService
{
    private const int MaxPageSize = 100;

    private readonly ApplicationDbContext _context = context;
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public async Task<Result<ClientsPageResponse>> GetAllAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        // The global !IsDeleted filter already excludes soft-deleted clients.
        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.FirstName.ToLower().Contains(term) ||
                x.LastName.ToLower().Contains(term) ||
                (x.Email != null && x.Email.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result.Success(new ClientsPageResponse(
            users.Select(MapClient).ToList(), page, pageSize, totalCount, totalPages));
    }

    public async Task<Result<ClientDetailResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
            return Result.Failure<ClientDetailResponse>(ClientErrors.ClientNotFound);

        var orderTotals = await _context.Orders.AsNoTracking()
            .Where(x => x.UserId == id)
            .Select(x => x.Total)
            .ToListAsync(cancellationToken);

        return Result.Success(new ClientDetailResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.PhoneNumber,
            IsActive(user),
            user.EmailConfirmed,
            orderTotals.Count,
            orderTotals.Sum()));
    }

    public async Task<Result<ClientResponse>> UpdateAsync(string id, UpdateClientRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
            return Result.Failure<ClientResponse>(ClientErrors.ClientNotFound);

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            // IgnoreQueryFilters on purpose: a soft-deleted account still occupies its
            // row in the unique index, so its email is NOT actually free for reuse.
            var normalized = _userManager.NormalizeEmail(request.Email);
            var taken = await _context.Users.IgnoreQueryFilters()
                .AnyAsync(x => x.Id != id && x.NormalizedEmail == normalized, cancellationToken);

            if (taken)
                return Result.Failure<ClientResponse>(ClientErrors.EmailAlreadyExists);

            // Never assign Email/UserName directly — UserManager keeps
            // NormalizedEmail/NormalizedUserName in sync, and login reads those.
            var emailResult = await _userManager.SetEmailAsync(user, request.Email);
            if (!emailResult.Succeeded)
                return Result.Failure<ClientResponse>(ClientErrors.EmailAlreadyExists);

            var userNameResult = await _userManager.SetUserNameAsync(user, request.Email);
            if (!userNameResult.Succeeded)
                return Result.Failure<ClientResponse>(ClientErrors.EmailAlreadyExists);
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return Result.Failure<ClientResponse>(ClientErrors.UpdateFailed);

        return Result.Success(MapClient(user));
    }

    public async Task<Result> ToggleStatusAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
            return Result.Failure(ClientErrors.ClientNotFound);

        // Disable/enable rides on Identity's built-in lockout, so login already
        // honours it and no auth code needs changing.
        if (IsActive(user))
        {
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
        }
        else
        {
            user.LockoutEnd = null;
        }

        var updateResult = await _userManager.UpdateAsync(user);
        return updateResult.Succeeded ? Result.Success() : Result.Failure(ClientErrors.UpdateFailed);
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
            return Result.Failure(ClientErrors.ClientNotFound);

        // The DbContext hook turns this into a soft delete. The client's orders stay
        // behind and remain readable via their ShipTo*/OrderItem snapshots, but
        // Include(o => o.User) returns null for them from now on — any admin view
        // that joins to the user must tolerate that and fall back to the snapshot.
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static bool IsActive(ApplicationUser user) =>
        user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow;

    private static ClientResponse MapClient(ApplicationUser user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email ?? string.Empty,
        user.PhoneNumber,
        IsActive(user),
        user.EmailConfirmed);
}
