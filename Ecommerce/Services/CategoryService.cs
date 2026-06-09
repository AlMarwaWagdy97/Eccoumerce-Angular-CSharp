using Ecommerce.Contracts.Categories;

namespace Ecommerce.Services;

public class CategoryService(ApplicationDbContext context) : ICategoryService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<Result<CategoryResponse>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FindAsync(new object[] { id }, cancellationToken);
        return category is not null
            ? Result.Success(category.Adapt<CategoryResponse>())
            : Result.Failure<CategoryResponse>(CategoryErrors.CategoryNotFound);
    }

    public async Task<Result<CategoryResponse>> AddAsync(CategoryRequest request, CancellationToken cancellationToken = default)
    {
        var isSlugExists = await _context.Categories.AnyAsync(x => x.Slug == request.Slug, cancellationToken);
        if (isSlugExists)
            return Result.Failure<CategoryResponse>(CategoryErrors.DuplicatedCategorySlug);

        var category = request.Adapt<Category>();

        category.Status = true;
        category.Feature = false;

        await _context.AddAsync(category, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(category.Adapt<CategoryResponse>());
    }

    public async Task<Result> UpdateAsync(long id, CategoryRequest request, CancellationToken cancellationToken = default)
    {
        var isSlugExists = await _context.Categories.AnyAsync(x => x.Slug == request.Slug && x.Id != id, cancellationToken);
        if (isSlugExists)
            return Result.Failure(CategoryErrors.DuplicatedCategorySlug);

        var category = await _context.Categories.FindAsync(new object[] { id }, cancellationToken);
        if (category is null)
            return Result.Failure(CategoryErrors.CategoryNotFound);

        request.Adapt(category); // تحديث الحقول مباشرة باستخدام Mapster

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FindAsync(new object[] { id }, cancellationToken);
        if (category is null)
            return Result.Failure(CategoryErrors.CategoryNotFound);

        _context.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FindAsync(new object[] { id }, cancellationToken);
        if (category is null)
            return Result.Failure(CategoryErrors.CategoryNotFound);

        category.Status = !category.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}