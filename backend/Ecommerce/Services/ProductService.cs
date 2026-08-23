using Microsoft.EntityFrameworkCore;
using Mapster;
using Ecommerce.Presistence;
using Ecommerce.Entities;
using Ecommerce.Contracts.Products;
using Ecommerce.Errors;
using Ecommerce.Abstractions;
using Ecommerce.Storage;

namespace Ecommerce.Services;

public class ProductService(ApplicationDbContext context, IFileStorage fileStorage) : IProductService
{
    private const string StorageModule = "products";
    private const int MaxPageSize = 100;

    private readonly ApplicationDbContext _context = context;
    private readonly IFileStorage _fileStorage = fileStorage;

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

    public async Task<Result<ProductsPageResponse>> GetAdminPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        // The global !IsDeleted filter already excludes soft-deleted products.
        var query = _context.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Title.ToLower().Contains(term) || x.Sku.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderBy(x => x.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return Result.Success(new ProductsPageResponse(
            products.Select(p => p.Adapt<ProductResponse>()).ToList(), page, pageSize, totalCount, totalPages));
    }

    public async Task<Result<ProductResponse>> AddAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var isSkuExists = await _context.Products.AnyAsync(x => x.Sku == request.Sku, cancellationToken);
        if (isSkuExists)
            return Result.Failure<ProductResponse>(ProductErrors.DuplicatedProductSku);

        var isSlugExists = await _context.Products.AnyAsync(x => x.Slug == request.Slug, cancellationToken);
        if (isSlugExists)
            return Result.Failure<ProductResponse>(ProductErrors.DuplicatedProductSlug);

        var imageResult = await ResolveImageAsync(request, currentImage: null, cancellationToken);
        if (!imageResult.IsSuccess)
            return Result.Failure<ProductResponse>(imageResult.Error);

        var product = new Product
        {
            CategoryId = request.CategoryId,
            Title = request.Title,
            Slug = request.Slug,
            Sku = request.Sku,
            Price = request.Price,
            Description = request.Description,
            Image = imageResult.Value,
            PriceAfterSale = request.PriceAfterSale,
            Sale = request.Sale,
            StockQuantity = request.StockQuantity ?? 0,
            Sort = request.Sort,
            Status = request.Status ?? true,
            Feature = request.Feature ?? false,
            MetaDescription = request.MetaDescription,
            MetaKey = request.MetaKey,
        };

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

        var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
            return Result.Failure<ProductResponse>(ProductErrors.ProductNotFound);

        var imageResult = await ResolveImageAsync(request, product.Image, cancellationToken);
        if (!imageResult.IsSuccess)
            return Result.Failure<ProductResponse>(imageResult.Error);

        product.CategoryId = request.CategoryId;
        product.Title = request.Title;
        product.Slug = request.Slug;
        product.Sku = request.Sku;
        product.Price = request.Price;
        product.Description = request.Description;
        product.Image = imageResult.Value;
        product.PriceAfterSale = request.PriceAfterSale;
        product.Sale = request.Sale;
        product.StockQuantity = request.StockQuantity ?? product.StockQuantity;
        product.Sort = request.Sort;
        product.Status = request.Status ?? product.Status;
        product.Feature = request.Feature ?? product.Feature;
        product.MetaDescription = request.MetaDescription;
        product.MetaKey = request.MetaKey;

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

    // ImageFile wins if present; otherwise a non-empty Image string wins;
    // otherwise the current stored path is kept unchanged.
    private async Task<Result<string?>> ResolveImageAsync(ProductRequest request, string? currentImage, CancellationToken cancellationToken)
    {
        if (request.ImageFile is not null)
        {
            var saved = await _fileStorage.SaveAsync(request.ImageFile, StorageModule, cancellationToken);
            return saved.IsSuccess
                ? Result.Success<string?>(saved.Value)
                : Result.Failure<string?>(saved.Error);
        }

        return Result.Success(string.IsNullOrWhiteSpace(request.Image) ? currentImage : request.Image);
    }
}
