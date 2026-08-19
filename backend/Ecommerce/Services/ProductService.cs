using Microsoft.EntityFrameworkCore;
using Mapster;
using Ecommerce.Presistence;
using Ecommerce.Entities;
using Ecommerce.Contracts.Products;
using Ecommerce.Errors;
using Ecommerce.Abstractions;

namespace Ecommerce.Services;

public class ProductService(ApplicationDbContext context) : IProductService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Products.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<Result<ProductResponse>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        return product is not null ?
            Result.Success(product.Adapt<ProductResponse>()) :
            Result.Failure<ProductResponse>(ProductErrors.ProductNotFound);
    }

    public async Task<Result<ProductDetailsResponse>> GetByIdOrSlugAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Images.OrderBy(i => i.Sort))
            .Include(p => p.Reviews)
                .ThenInclude(r => r.User)
            .AsQueryable();

        var product = long.TryParse(identifier, out long id)
            ? await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(p => p.Slug == identifier, cancellationToken);

        if (product is null)
            return Result.Failure<ProductDetailsResponse>(ProductErrors.ProductNotFound);

        var reviews = product.Reviews
            .OrderByDescending(r => r.CreatedOn)
            .Select(r => new ProductReviewResponse(
                r.Id,
                $"{r.User.FirstName} {r.User.LastName}".Trim(),
                r.Rating,
                r.Comment,
                r.CreatedOn))
            .ToList();

        var response = new ProductDetailsResponse(
            product.Id,
            product.CategoryId,
            product.Category?.Title,
            product.Title,
            product.Slug,
            product.Sku,
            product.Price,
            product.PriceAfterSale,
            product.Sale,
            product.Description,
            product.Image,
            product.Images.Select(i => i.Url).ToList(),
            product.StockQuantity,
            reviews.Count > 0 ? reviews.Average(r => r.Rating) : null,
            reviews.Count,
            reviews,
            product.MetaDescription,
            product.MetaKey);

        return Result.Success(response);
    }

    public async Task<Result<ProductResponse>> AddAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var isSkuExists = await _context.Products.AnyAsync(x => x.Sku == request.Sku, cancellationToken);
        if (isSkuExists)
            return Result.Failure<ProductResponse>(ProductErrors.DuplicatedProductSku);

        var isSlugExists = await _context.Products.AnyAsync(x => x.Slug == request.Slug, cancellationToken);
        if (isSlugExists)
            return Result.Failure<ProductResponse>(ProductErrors.DuplicatedProductSlug);

        var product = request.Adapt<Product>();
        product.Status = true;
        product.Feature = false;

        await _context.AddAsync(product, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(product.Adapt<ProductResponse>());
    }

    public async Task<Result<ProductResponse>> UpdateAsync(long id, ProductRequest request, CancellationToken cancellationToken = default)
    {
        var isSkuExists = await _context.Products.AnyAsync(x => x.Sku == request.Sku && x.Id != id, cancellationToken);
        if (isSkuExists)
            return Result.Failure<ProductResponse>(ProductErrors.DuplicatedProductSku);

        var isSlugExists = await _context.Products.AnyAsync(x => x.Slug == request.Slug && x.Id != id, cancellationToken);
        if (isSlugExists)
            return Result.Failure<ProductResponse>(ProductErrors.DuplicatedProductSlug);

        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product is null)
            return Result.Failure<ProductResponse>(ProductErrors.ProductNotFound);

        request.Adapt(product);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(product.Adapt<ProductResponse>());
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.ProductNotFound);

        // The product row survives as IsDeleted, so anything still pointing at it would keep
        // showing a product customers can no longer buy. Favorite and CartItem are deliberately
        // not auditable, so removing them here is a real delete. OrderItem is left alone: it
        // snapshots the product details and is history.
        var favorites = await _context.Favorites
            .Where(f => f.ProductId == id)
            .ToListAsync(cancellationToken);
        _context.Favorites.RemoveRange(favorites);

        var cartItems = await _context.CartItems
            .Where(c => c.ProductId == id)
            .ToListAsync(cancellationToken);
        _context.CartItems.RemoveRange(cartItems);

        _context.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product is null)
            return Result.Failure(ProductErrors.ProductNotFound);

        product.Status = !product.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}