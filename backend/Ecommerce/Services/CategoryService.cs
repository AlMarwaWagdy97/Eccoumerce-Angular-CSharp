using Ecommerce.Contracts.Categories;
using Ecommerce.Storage;

namespace Ecommerce.Services;

public class CategoryService(ApplicationDbContext context, IFileStorage fileStorage) : ICategoryService
{
    private const string StorageModule = "categories";

    private readonly ApplicationDbContext _context = context;
    private readonly IFileStorage _fileStorage = fileStorage;

    public async Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Categories.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<Result<CategoryResponse>> GetAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return category is not null
            ? Result.Success(category.Adapt<CategoryResponse>())
            : Result.Failure<CategoryResponse>(CategoryErrors.CategoryNotFound);
    }

    public async Task<Result<CategoryResponse>> AddAsync(CategoryRequest request, CancellationToken cancellationToken = default)
    {
        // The global !IsDeleted filter means a soft-deleted category's slug is free again.
        var isSlugExists = await _context.Categories.AnyAsync(x => x.Slug == request.Slug, cancellationToken);
        if (isSlugExists)
            return Result.Failure<CategoryResponse>(CategoryErrors.DuplicatedCategorySlug);

        var imageResult = await ResolveImageAsync(request, currentImage: null, cancellationToken);
        if (!imageResult.IsSuccess)
            return Result.Failure<CategoryResponse>(imageResult.Error);

        var category = new Category
        {
            ParentId = request.ParentId,
            Title = request.Title,
            Slug = request.Slug,
            Description = request.Description,
            Image = imageResult.Value,
            Sort = request.Sort,
            MetaDescription = request.MetaDescription,
            MetaKey = request.MetaKey,
            Feature = request.Feature ?? false,
            Status = request.Status ?? true,
        };

        await _context.AddAsync(category, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(category.Adapt<CategoryResponse>());
    }

    public async Task<Result<CategoryResponse>> UpdateAsync(long id, CategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ParentId == id)
            return Result.Failure<CategoryResponse>(CategoryErrors.InvalidParent);

        var isSlugExists = await _context.Categories.AnyAsync(x => x.Slug == request.Slug && x.Id != id, cancellationToken);
        if (isSlugExists)
            return Result.Failure<CategoryResponse>(CategoryErrors.DuplicatedCategorySlug);

        var category = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
            return Result.Failure<CategoryResponse>(CategoryErrors.CategoryNotFound);

        var imageResult = await ResolveImageAsync(request, category.Image, cancellationToken);
        if (!imageResult.IsSuccess)
            return Result.Failure<CategoryResponse>(imageResult.Error);

        category.ParentId = request.ParentId;
        category.Title = request.Title;
        category.Slug = request.Slug;
        category.Description = request.Description;
        category.Image = imageResult.Value;
        category.Sort = request.Sort;
        category.MetaDescription = request.MetaDescription;
        category.MetaKey = request.MetaKey;
        category.Feature = request.Feature ?? category.Feature;
        category.Status = request.Status ?? category.Status;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(category.Adapt<CategoryResponse>());
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
            return Result.Failure(CategoryErrors.CategoryNotFound);

        // The DbContext hook turns this into a soft delete — no IsDeleted assignment here.
        _context.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (category is null)
            return Result.Failure(CategoryErrors.CategoryNotFound);

        category.Status = !category.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    // ImageFile wins if present; otherwise a non-empty Image string wins;
    // otherwise the current stored path is kept unchanged.
    private async Task<Result<string?>> ResolveImageAsync(CategoryRequest request, string? currentImage, CancellationToken cancellationToken)
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
