using Microsoft.EntityFrameworkCore;
using Ecommerce.Contracts.Addresses;

namespace Ecommerce.Services;

public class AddressService(ApplicationDbContext context) : IAddressService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Result<IEnumerable<AddressResponse>>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var addresses = await _context.Addresses
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<AddressResponse>>(addresses.Select(MapAddress).ToList());
    }

    public async Task<Result<AddressResponse>> AddAsync(string userId, AddressRequest request, CancellationToken cancellationToken = default)
    {
        var isFirstAddress = !await _context.Addresses.AnyAsync(x => x.UserId == userId, cancellationToken);
        var makeDefault = request.IsDefault || isFirstAddress;

        if (makeDefault)
            await ClearDefaultAsync(userId, cancellationToken);

        var address = new Address
        {
            UserId = userId,
            FullName = request.FullName,
            Phone = request.Phone,
            Line1 = request.Line1,
            Line2 = request.Line2,
            City = request.City,
            State = request.State,
            Country = request.Country,
            PostalCode = request.PostalCode,
            IsDefault = makeDefault
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapAddress(address));
    }

    public async Task<Result<AddressResponse>> UpdateAsync(string userId, long id, AddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (address is null)
            return Result.Failure<AddressResponse>(AddressErrors.AddressNotFound);

        if (request.IsDefault && !address.IsDefault)
            await ClearDefaultAsync(userId, cancellationToken);

        address.FullName = request.FullName;
        address.Phone = request.Phone;
        address.Line1 = request.Line1;
        address.Line2 = request.Line2;
        address.City = request.City;
        address.State = request.State;
        address.Country = request.Country;
        address.PostalCode = request.PostalCode;
        address.IsDefault = request.IsDefault;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapAddress(address));
    }

    public async Task<Result> DeleteAsync(string userId, long id, CancellationToken cancellationToken = default)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (address is null)
            return Result.Failure(AddressErrors.AddressNotFound);

        _context.Addresses.Remove(address);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> SetDefaultAsync(string userId, long id, CancellationToken cancellationToken = default)
    {
        var address = await _context.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (address is null)
            return Result.Failure(AddressErrors.AddressNotFound);

        await ClearDefaultAsync(userId, cancellationToken);
        address.IsDefault = true;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task ClearDefaultAsync(string userId, CancellationToken cancellationToken)
    {
        var currentDefaults = await _context.Addresses
            .Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var address in currentDefaults)
            address.IsDefault = false;
    }

    private static AddressResponse MapAddress(Address x) => new(
        x.Id, x.FullName, x.Phone, x.Line1, x.Line2, x.City, x.State, x.Country, x.PostalCode, x.IsDefault);
}
