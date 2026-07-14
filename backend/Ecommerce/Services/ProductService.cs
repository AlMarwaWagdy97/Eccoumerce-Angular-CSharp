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

    public async Task<Result<ProductResponse>> GetByIdOrSlugAsync(string identifier, CancellationToken cancellationToken = default)
    {
        Product? product = null;

        if (long.TryParse(identifier, out long id))
        {
            product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }
        else
        {
            product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == identifier, cancellationToken);
        }
        return product is not null ?
            Result.Success(product.Adapt<ProductResponse>()) :
            Result.Failure<ProductResponse>(ProductErrors.ProductNotFound);
    }

    public async Task<Result<ProductResponse>> AddAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var isSkuExists = await _context.Products.AnyAsync(x => x.Sku == request.Sku, cancellationToken);
        if (isSkuExists)
            return Result.Failure<ProductResponse>(ProductErrors.DuplicatedProductSku);

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