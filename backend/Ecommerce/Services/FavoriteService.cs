using Microsoft.EntityFrameworkCore;
using Ecommerce.Contracts.Favorites;

namespace Ecommerce.Services;

public class FavoriteService(ApplicationDbContext context) : IFavoriteService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Result<IEnumerable<FavoriteResponse>>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var favorites = await _context.Favorites
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new FavoriteResponse(
                x.Id,
                x.ProductId,
                x.Product.Title,
                x.Product.Image,
                x.Product.PriceAfterSale ?? x.Product.Price,
                x.Product.Slug))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<FavoriteResponse>>(favorites);
    }

    public async Task<Result> AddAsync(string userId, long productId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Favorites.AnyAsync(x => x.UserId == userId && x.ProductId == productId, cancellationToken);
        if (exists)
            return Result.Success();

        var product = await _context.Products.FindAsync(new object[] { productId }, cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.ProductNotFound);

        _context.Favorites.Add(new Favorite { UserId = userId, ProductId = productId });
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RemoveAsync(string userId, long productId, CancellationToken cancellationToken = default)
    {
        var favorite = await _context.Favorites.FirstOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId, cancellationToken);
        if (favorite is null)
            return Result.Failure(FavoriteErrors.FavoriteNotFound);

        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
