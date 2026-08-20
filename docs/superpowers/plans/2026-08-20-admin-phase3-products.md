# Phase 3 — Products Admin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give admins full CRUD over the product catalog — cover image, a real multi-image gallery, stock, and status/feature control — mirroring the Categories/Clients/Sliders admin pattern from Phase 2B.

**Architecture:** A new `AdminProductsController` (gated `products.view`/`products.manage`) takes over all writes and admin reads from the existing `ProductService`, which gains cover-image upload, gallery add/delete, and paged/searchable admin listing. The public `ProductsController` trims to its two existing read actions. Frontend gets a new `admin/features/pages/products/` page (table + search/pagination + form + gallery) following the Clients page's shape.

**Tech Stack:** ASP.NET Core 10 / EF Core / FluentValidation / Mapster (backend), Angular 22 standalone components + signals (frontend). No new packages, no new migration.

**Spec:** `docs/superpowers/specs/2026-08-20-admin-phase3-products-design.md`

## Global Constraints

- No EF migration in this plan — `ProductImage` already inherits `AuditableEntity`; `Product.StockQuantity` already exists as a column.
- No service takes an `adminId` parameter — audit stamping and soft-delete come from the `SaveChanges` hook (Plan 2A), same as every other admin-managed entity.
- Public `ProductsController` keeps only `GET ""`/`GET "{slug}"`; every write and admin read moves to the new `AdminProductsController` at `api/Admin/Products`.
- Cover image and gallery images both use `IFileStorage` module `"products"`.
- `ProductImageResponse`/`AdminProductDetailResponse` gallery lists are always ordered by `Sort` ascending.

---

## Task 1: `ProductService` — image upload, stock/status/feature, admin search, validator

**Files:**
- Modify: `backend/Ecommerce/Contracts/Products/ProductRequest.cs`
- Modify: `backend/Ecommerce/Contracts/Products/ProductResponse.cs`
- Create: `backend/Ecommerce/Contracts/Products/ProductsPageResponse.cs`
- Modify: `backend/Ecommerce/Services/IProductService.cs`
- Modify: `backend/Ecommerce/Services/ProductService.cs`
- Modify: `backend/Ecommerce.Tests/Services/ProductSoftDeleteCleanupTests.cs`
- Test: `backend/Ecommerce.Tests/Services/ProductServiceTests.cs`

**Interfaces:**
- Consumes: `Ecommerce.Storage.IFileStorage.SaveAsync(IFormFile file, string module, CancellationToken)` → `Task<Result<string>>` (Plan 2A); `Ecommerce.Tests.StubFileStorage` and `Ecommerce.Tests.TestFiles.Image(...)` (Plan 2B Task 1, already exist — reused as-is, no changes).
- Produces: `ProductRequest(long CategoryId, string Title, string Slug, string Sku, double Price, string? Description, string? Image, double? PriceAfterSale, double? Sale, int? StockQuantity, int? Sort, bool? Status, bool? Feature, string? MetaDescription, string? MetaKey, IFormFile? ImageFile = null)` — Task 3's `AdminProductsController` binds this with `[FromForm]`, Task 4's frontend posts a `FormData` whose field names match these property names.
- Produces: `ProductResponse` gains `int StockQuantity`.
- Produces: `ProductsPageResponse(IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages)`.
- Produces: `ProductService(ApplicationDbContext context, IFileStorage fileStorage)` — the constructor gains a second parameter; `DependacyInjection.cs` already reads `services.AddScoped<IProductService, ProductService>()` and needs no change (verify in Step 8).
- Produces: `IProductService.GetAdminPageAsync(string? search, int page, int pageSize, CancellationToken)` → `Task<Result<ProductsPageResponse>>`.
- `ICategoryService`... (n/a) — `IProductService`'s other method signatures are **unchanged** in this task: `GetAllAsync`, `GetAsync(long)`, `GetByIdOrSlugAsync(string)`, `AddAsync(ProductRequest)`, `UpdateAsync(long, ProductRequest)`, `DeleteAsync(long)`, `ToggleStatusAsync(long)`. Task 2 adds three more methods to this same interface.

- [ ] **Step 1: Update the contracts**

Replace the whole file:

```csharp
// backend/Ecommerce/Contracts/Products/ProductRequest.cs
namespace Ecommerce.Contracts.Products
{
    // ImageFile is the multipart upload; Image is the already-stored path.
    // If ImageFile is present it wins and its saved path replaces Image;
    // otherwise Image is kept as-is, which is how "leave the current image
    // alone" is expressed on an update. StockQuantity/Status/Feature are
    // nullable so an update that omits them keeps the current value.
    public record class ProductRequest(
        long CategoryId,
        string Title,
        string Slug,
        string Sku,
        double Price,
        string? Description,
        string? Image,
        double? PriceAfterSale,
        double? Sale,
        int? StockQuantity,
        int? Sort,
        bool? Status,
        bool? Feature,
        string? MetaDescription,
        string? MetaKey,
        IFormFile? ImageFile = null
    );

    public class ProductRequestValidator : AbstractValidator<ProductRequest>
    {
        public ProductRequestValidator()
        {
            RuleFor(x => x.CategoryId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Price).GreaterThan(0);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.MetaDescription).MaximumLength(500);
            RuleFor(x => x.MetaKey).MaximumLength(255);
        }
    }
}
```

(The pre-existing `ProductRequestValidation : AbstractValidator<Product>` in the sibling file validates the *entity*, not this record, and no action binds a `Product` directly — leave that file alone, same situation Categories Task 1 found and left alone. SKU/slug uniqueness and category-existence stay enforced in the service, unchanged below — the new validator only adds the structural checks that were previously enforced nowhere for the actual bound DTO.)

```csharp
// backend/Ecommerce/Contracts/Products/ProductResponse.cs
namespace Ecommerce.Contracts.Products
{
    public record class ProductResponse(
        long Id,
        long CategoryId,
        string Title,
        string Slug,
        string Sku,
        double Price,
        double? PriceAfterSale,
        double? Sale,
        string? Image,
        int StockQuantity,
        int? Sort,
        bool Feature,
        bool Status,
        string? MetaDescription,
        string? MetaKey
    );
}
```

```csharp
// backend/Ecommerce/Contracts/Products/ProductsPageResponse.cs
namespace Ecommerce.Contracts.Products;

public record ProductsPageResponse(
    IReadOnlyList<ProductResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

- [ ] **Step 2: Add `GetAdminPageAsync` to the interface**

```csharp
// backend/Ecommerce/Services/IProductService.cs
using Ecommerce.Contracts.Products;

namespace Ecommerce.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<Result<ProductResponse>> GetAsync(long id, CancellationToken cancellationToken = default);
        Task<Result<ProductDetailsResponse>> GetByIdOrSlugAsync(string identifier, CancellationToken cancellationToken = default);
        Task<Result<ProductsPageResponse>> GetAdminPageAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<Result<ProductResponse>> AddAsync(ProductRequest request, CancellationToken cancellationToken = default);
        Task<Result<ProductResponse>> UpdateAsync(long id, ProductRequest request, CancellationToken cancellationToken = default);
        Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
        Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default);
    }
}
```

- [ ] **Step 3: Fix the pre-existing test that constructs `ProductService` directly**

`ProductSoftDeleteCleanupTests.cs` predates this task and calls `new ProductService(context)` four times. Once Step 6 below changes the constructor, these calls stop compiling. Fix them now so the build stays green start-to-finish:

In `backend/Ecommerce.Tests/Services/ProductSoftDeleteCleanupTests.cs`, change every occurrence of:

```csharp
new ProductService(context)
```

to:

```csharp
new ProductService(context, new StubFileStorage())
```

There are four occurrences (`Deleting_a_product_drops_it_from_every_favorites_list`, `Deleting_a_product_drops_it_from_every_cart`, `The_product_itself_is_only_soft_deleted`, `Order_history_still_references_the_deleted_product`). `StubFileStorage` is already in scope — it lives in the root `Ecommerce.Tests` namespace (`backend/Ecommerce.Tests/StubFileStorage.cs`, added by Plan 2B Task 1), visible from `Ecommerce.Tests.Services` without a `using`, same as `NoopHttpContextAccessor` already is in this file.

- [ ] **Step 4: Write the failing tests**

```csharp
// backend/Ecommerce.Tests/Services/ProductServiceTests.cs
using Ecommerce.Contracts.Products;
using Ecommerce.Entities;
using Ecommerce.Errors;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class ProductServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<long> SeedCategoryAsync(ApplicationDbContext context, string title = "Shoes")
    {
        var category = new Category { Title = title, Slug = title.ToLower() };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static ProductRequest Request(
        long categoryId,
        string title = "Runner",
        string slug = "runner",
        string sku = "SKU-1",
        double price = 50,
        string? image = null,
        int? stockQuantity = null,
        bool? status = null,
        bool? feature = null,
        IFormFile? imageFile = null) =>
        new(categoryId, title, slug, sku, price, null, image, null, null, stockQuantity, 1, status, feature, null, null, imageFile);

    [Fact]
    public async Task AddAsync_saves_the_uploaded_image_and_uses_its_path()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var storage = new StubFileStorage("/uploads/products/hero.jpg");
        var service = new ProductService(context, storage);

        var result = await service.AddAsync(Request(categoryId, imageFile: TestFiles.Image()));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/products/hero.jpg", result.Value.Image);
        Assert.Equal("products", storage.LastModule);
    }

    [Fact]
    public async Task AddAsync_keeps_the_supplied_image_string_when_no_file_is_uploaded()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());

        var result = await service.AddAsync(Request(categoryId, image: "/uploads/products/seeded.png"));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/products/seeded.png", result.Value.Image);
    }

    [Fact]
    public async Task AddAsync_honours_stock_status_and_feature_instead_of_hardcoding_them()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());

        var result = await service.AddAsync(Request(categoryId, stockQuantity: 25, status: false, feature: true));

        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Value.StockQuantity);
        Assert.False(result.Value.Status);
        Assert.True(result.Value.Feature);
    }

    [Fact]
    public async Task AddAsync_defaults_stock_to_zero_and_status_to_active_when_omitted()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());

        var result = await service.AddAsync(Request(categoryId));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.StockQuantity);
        Assert.True(result.Value.Status);
        Assert.False(result.Value.Feature);
    }

    [Fact]
    public async Task AddAsync_propagates_a_file_storage_failure()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage(failWith: FileErrors.UnsupportedType));

        var result = await service.AddAsync(Request(categoryId, imageFile: TestFiles.Image("virus.exe")));

        Assert.False(result.IsSuccess);
        Assert.Equal("File.UnsupportedType", result.Error.Code);
        Assert.False(await context.Products.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_keeps_the_existing_image_when_neither_a_file_nor_an_image_path_is_supplied()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request(categoryId, image: "/uploads/products/original.jpg"))).Value;

        var result = await service.UpdateAsync(created.Id, Request(categoryId, title: "Renamed", slug: "renamed", sku: created.Sku));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value.Title);
        Assert.Equal("/uploads/products/original.jpg", result.Value.Image);
    }

    [Fact]
    public async Task UpdateAsync_keeps_the_existing_stock_and_status_when_omitted()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request(categoryId, stockQuantity: 40, status: false))).Value;

        var result = await service.UpdateAsync(created.Id, Request(categoryId, title: "Renamed", sku: created.Sku));

        Assert.True(result.IsSuccess);
        Assert.Equal(40, result.Value.StockQuantity);
        Assert.False(result.Value.Status);
    }

    [Fact]
    public async Task GetAdminPageAsync_filters_by_title_or_sku()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        await service.AddAsync(Request(categoryId, title: "Blue Runner", slug: "blue-runner", sku: "RUN-BLU"));
        await service.AddAsync(Request(categoryId, title: "Red Sandals", slug: "red-sandals", sku: "SAN-RED"));

        var result = await service.GetAdminPageAsync("run", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal("Blue Runner", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task GetAdminPageAsync_pages_the_result_set()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        await service.AddAsync(Request(categoryId, title: "A", slug: "a", sku: "SKU-A"));
        await service.AddAsync(Request(categoryId, title: "B", slug: "b", sku: "SKU-B"));
        await service.AddAsync(Request(categoryId, title: "C", slug: "c", sku: "SKU-C"));

        var result = await service.GetAdminPageAsync(null, 2, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
        Assert.Single(result.Value.Items);
    }
}
```

- [ ] **Step 5: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter ProductServiceTests
```

Expected: does not compile — `ProductService` still has a one-parameter constructor, `GetAdminPageAsync` doesn't exist, and `ProductRequest`'s constructor shape doesn't match yet.

- [ ] **Step 6: Rewrite `ProductService`**

Replace the whole file:

```csharp
// backend/Ecommerce/Services/ProductService.cs
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
```

Note: `DeleteAsync`/`ToggleStatusAsync`/`GetAsync` keep using `FindAsync` unchanged — that pre-dates this plan and is out of scope here (Categories Task 1 switched to `FirstOrDefaultAsync` for a reason specific to that task; Products' `GetByIdOrSlugAsync` already builds its own filtered query and doesn't go through `FindAsync` at all, so nothing in this plan actually depends on the distinction).

- [ ] **Step 7: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter ProductServiceTests
```

Expected: 9 passed.

- [ ] **Step 8: Run the whole suite and build the solution**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
dotnet build Ecommerce.slnx
```

Expected: all tests pass (115 = 106 + 9 new), including `ProductSoftDeleteCleanupTests` and `ProductsControllerAuthorizationTests` (both still reference the old single-controller shape — `ProductsControllerAuthorizationTests` keeps passing here because `ProductsController` hasn't been trimmed yet; Task 3 updates it). 0 build errors.

- [ ] **Step 9: Commit**

```bash
git add backend/Ecommerce/Contracts/Products/ProductRequest.cs backend/Ecommerce/Contracts/Products/ProductResponse.cs backend/Ecommerce/Contracts/Products/ProductsPageResponse.cs backend/Ecommerce/Services/IProductService.cs backend/Ecommerce/Services/ProductService.cs backend/Ecommerce.Tests/Services/ProductSoftDeleteCleanupTests.cs backend/Ecommerce.Tests/Services/ProductServiceTests.cs
git commit -m "Add image upload, stock/status/feature, and admin search to ProductService"
```

---

## Task 2: Product image gallery

**Files:**
- Create: `backend/Ecommerce/Contracts/Products/ProductImageResponse.cs`
- Create: `backend/Ecommerce/Contracts/Products/AdminProductDetailResponse.cs`
- Modify: `backend/Ecommerce/Errors/ProductErrors.cs`
- Modify: `backend/Ecommerce/Services/IProductService.cs`
- Modify: `backend/Ecommerce/Services/ProductService.cs`
- Test: `backend/Ecommerce.Tests/Services/ProductServiceTests.cs`

**Interfaces:**
- Consumes: `ApplicationDbContext.ProductImages` (`DbSet<ProductImage>`, already exists); `Entities.ProductImage { long Id, long ProductId, string Url, int Sort }` (already exists, already `AuditableEntity`).
- Produces: `ProductImageResponse(long Id, string Url, int Sort)`.
- Produces: `AdminProductDetailResponse(long Id, long CategoryId, string Title, string Slug, string Sku, double Price, double? PriceAfterSale, double? Sale, string? Description, string? Image, IReadOnlyList<ProductImageResponse> Images, int StockQuantity, int? Sort, bool Feature, bool Status, string? MetaDescription, string? MetaKey)` — Task 3's `AdminProductsController.GetByIdAsync` returns this.
- Produces: `ProductErrors.ProductImageNotFound` (code `"Product.ImageNotFound"`).
- Produces on `IProductService`: `GetAdminDetailAsync(long id, CancellationToken)` → `Task<Result<AdminProductDetailResponse>>`; `AddImagesAsync(long productId, IReadOnlyList<IFormFile> files, CancellationToken)` → `Task<Result<IReadOnlyList<ProductImageResponse>>>`; `DeleteImageAsync(long productId, long imageId, CancellationToken)` → `Task<Result>`.

- [ ] **Step 1: Write the new contracts and error**

```csharp
// backend/Ecommerce/Contracts/Products/ProductImageResponse.cs
namespace Ecommerce.Contracts.Products;

public record ProductImageResponse(long Id, string Url, int Sort);
```

```csharp
// backend/Ecommerce/Contracts/Products/AdminProductDetailResponse.cs
namespace Ecommerce.Contracts.Products;

public record AdminProductDetailResponse(
    long Id,
    long CategoryId,
    string Title,
    string Slug,
    string Sku,
    double Price,
    double? PriceAfterSale,
    double? Sale,
    string? Description,
    string? Image,
    IReadOnlyList<ProductImageResponse> Images,
    int StockQuantity,
    int? Sort,
    bool Feature,
    bool Status,
    string? MetaDescription,
    string? MetaKey);
```

In `backend/Ecommerce/Errors/ProductErrors.cs`, add one line inside the existing class:

```csharp
        public static readonly Error ProductImageNotFound = new("Product.ImageNotFound", "No image was found with the given ID for this product");
```

- [ ] **Step 2: Add the three methods to the interface**

In `backend/Ecommerce/Services/IProductService.cs`, add inside the interface (after `GetAdminPageAsync`, before `AddAsync`):

```csharp
        Task<Result<AdminProductDetailResponse>> GetAdminDetailAsync(long id, CancellationToken cancellationToken = default);
```

and at the end of the interface (after `ToggleStatusAsync`):

```csharp
        Task<Result<IReadOnlyList<ProductImageResponse>>> AddImagesAsync(long productId, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken = default);
        Task<Result> DeleteImageAsync(long productId, long imageId, CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Write the failing tests**

Append to `backend/Ecommerce.Tests/Services/ProductServiceTests.cs`, inside the `ProductServiceTests` class:

```csharp
    [Fact]
    public async Task GetAdminDetailAsync_returns_the_gallery_ordered_by_sort()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var storage = new StubFileStorage();
        var service = new ProductService(context, storage);
        var created = (await service.AddAsync(Request(categoryId))).Value;

        storage.SetNextPath("/uploads/products/b.jpg");
        await service.AddImagesAsync(created.Id, new[] { TestFiles.Image("b.jpg") });
        storage.SetNextPath("/uploads/products/a.jpg");
        await service.AddImagesAsync(created.Id, new[] { TestFiles.Image("a.jpg") });

        var result = await service.GetAdminDetailAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Images.Count);
        Assert.Equal("/uploads/products/b.jpg", result.Value.Images[0].Url);
        Assert.Equal("/uploads/products/a.jpg", result.Value.Images[1].Url);
        Assert.True(result.Value.Images[0].Sort < result.Value.Images[1].Sort);
    }

    [Fact]
    public async Task GetAdminDetailAsync_fails_for_an_unknown_product()
    {
        await using var context = CreateContext();
        var service = new ProductService(context, new StubFileStorage());

        var result = await service.GetAdminDetailAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Equal("Product.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddImagesAsync_saves_every_file_and_increments_sort()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var storage = new StubFileStorage("/uploads/products/first.jpg");
        var service = new ProductService(context, storage);
        var created = (await service.AddAsync(Request(categoryId))).Value;

        var result = await service.AddImagesAsync(created.Id, new[] { TestFiles.Image("a.jpg"), TestFiles.Image("b.jpg") });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(2, storage.SaveCallCount);
        Assert.True(result.Value[1].Sort > result.Value[0].Sort);
    }

    [Fact]
    public async Task AddImagesAsync_fails_for_an_unknown_product()
    {
        await using var context = CreateContext();
        var service = new ProductService(context, new StubFileStorage());

        var result = await service.AddImagesAsync(999, new[] { TestFiles.Image() });

        Assert.False(result.IsSuccess);
        Assert.Equal("Product.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddImagesAsync_fails_when_no_files_are_supplied()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request(categoryId))).Value;

        var result = await service.AddImagesAsync(created.Id, Array.Empty<IFormFile>());

        Assert.False(result.IsSuccess);
        Assert.Equal("File.Empty", result.Error.Code);
    }

    [Fact]
    public async Task DeleteImageAsync_removes_the_image_from_the_gallery()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request(categoryId))).Value;
        var added = (await service.AddImagesAsync(created.Id, new[] { TestFiles.Image() })).Value;

        var result = await service.DeleteImageAsync(created.Id, added[0].Id);

        Assert.True(result.IsSuccess);
        var detail = await service.GetAdminDetailAsync(created.Id);
        Assert.Empty(detail.Value.Images);
    }

    [Fact]
    public async Task DeleteImageAsync_fails_for_an_image_belonging_to_another_product()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        var first = (await service.AddAsync(Request(categoryId, title: "First", slug: "first", sku: "SKU-F"))).Value;
        var second = (await service.AddAsync(Request(categoryId, title: "Second", slug: "second", sku: "SKU-S"))).Value;
        var added = (await service.AddImagesAsync(first.Id, new[] { TestFiles.Image() })).Value;

        var result = await service.DeleteImageAsync(second.Id, added[0].Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Product.ImageNotFound", result.Error.Code);
    }
```

`StubFileStorage` needs a small addition to support the ordering test above — it currently always returns the same fixed path, which would make two uploaded images collide on `Url`. Modify `backend/Ecommerce.Tests/StubFileStorage.cs`:

```csharp
// backend/Ecommerce.Tests/StubFileStorage.cs
using Ecommerce.Abstractions;
using Ecommerce.Storage;
using Microsoft.AspNetCore.Http;

namespace Ecommerce.Tests;

// Test double for Plan 2A's IFileStorage: records what it was called with and
// returns a deterministic path, so services can be tested without touching disk.
public class StubFileStorage(string savedPath = "/uploads/test/stub.jpg", Error? failWith = null) : IFileStorage
{
    private string _savedPath = savedPath;
    private readonly Error? _failWith = failWith;

    public string? LastModule { get; private set; }
    public int SaveCallCount { get; private set; }

    // Lets a single stub return a different path on the next call — needed when a
    // test uploads more than one file and must tell the results apart by URL.
    public void SetNextPath(string path) => _savedPath = path;

    public Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken cancellationToken = default)
    {
        LastModule = module;
        SaveCallCount++;

        return Task.FromResult(_failWith is null
            ? Result.Success(_savedPath)
            : Result.Failure<string>(_failWith));
    }
}

public static class TestFiles
{
    public static IFormFile Image(string fileName = "photo.jpg")
    {
        var bytes = new byte[] { 1, 2, 3, 4 };

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ImageFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg",
        };
    }
}
```

(Only change: `_savedPath` goes from `readonly` to a plain field, plus the new `SetNextPath` method. Every existing caller across `CategoryServiceTests`/`SliderServiceTests`/`ProductServiceTests` that never calls `SetNextPath` keeps behaving exactly as before.)

- [ ] **Step 4: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter ProductServiceTests
```

Expected: does not compile — `GetAdminDetailAsync`/`AddImagesAsync`/`DeleteImageAsync`/`SetNextPath` don't exist yet.

- [ ] **Step 5: Add the three methods to `ProductService`**

In `backend/Ecommerce/Services/ProductService.cs`, add these three methods (after `GetAdminPageAsync`, before `AddAsync`, for `GetAdminDetailAsync`; the other two go at the end of the class, after `ToggleStatusAsync` and before the private `ResolveImageAsync`):

```csharp
    public async Task<Result<AdminProductDetailResponse>> GetAdminDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.AsNoTracking()
            .Include(p => p.Images.OrderBy(i => i.Sort))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (product is null)
            return Result.Failure<AdminProductDetailResponse>(ProductErrors.ProductNotFound);

        var response = new AdminProductDetailResponse(
            product.Id,
            product.CategoryId,
            product.Title,
            product.Slug,
            product.Sku,
            product.Price,
            product.PriceAfterSale,
            product.Sale,
            product.Description,
            product.Image,
            product.Images.Select(i => new ProductImageResponse(i.Id, i.Url, i.Sort)).ToList(),
            product.StockQuantity,
            product.Sort,
            product.Feature,
            product.Status,
            product.MetaDescription,
            product.MetaKey);

        return Result.Success(response);
    }
```

```csharp
    public async Task<Result<IReadOnlyList<ProductImageResponse>>> AddImagesAsync(long productId, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null)
            return Result.Failure<IReadOnlyList<ProductImageResponse>>(ProductErrors.ProductNotFound);

        if (files.Count == 0)
            return Result.Failure<IReadOnlyList<ProductImageResponse>>(FileErrors.EmptyFile);

        var maxSort = await _context.ProductImages
            .Where(x => x.ProductId == productId)
            .Select(x => (int?)x.Sort)
            .MaxAsync(cancellationToken) ?? 0;

        var added = new List<ProductImage>();
        foreach (var file in files)
        {
            var saved = await _fileStorage.SaveAsync(file, StorageModule, cancellationToken);
            if (!saved.IsSuccess)
                return Result.Failure<IReadOnlyList<ProductImageResponse>>(saved.Error);

            maxSort++;
            var image = new ProductImage { ProductId = productId, Url = saved.Value, Sort = maxSort };
            added.Add(image);
            await _context.ProductImages.AddAsync(image, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ProductImageResponse>>(
            added.Select(i => new ProductImageResponse(i.Id, i.Url, i.Sort)).ToList());
    }

    public async Task<Result> DeleteImageAsync(long productId, long imageId, CancellationToken cancellationToken = default)
    {
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(x => x.Id == imageId && x.ProductId == productId, cancellationToken);
        if (image is null)
            return Result.Failure(ProductErrors.ProductImageNotFound);

        // The DbContext hook turns this into a soft delete — ProductImage is
        // AuditableEntity, same as every other Phase 2B/3 upload-backed entity.
        _context.Remove(image);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
```

- [ ] **Step 6: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter ProductServiceTests
```

Expected: 16 passed (9 from Task 1 + 7 new).

- [ ] **Step 7: Build the solution**

```powershell
dotnet build Ecommerce.slnx
```

Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add backend/Ecommerce/Contracts/Products/ProductImageResponse.cs backend/Ecommerce/Contracts/Products/AdminProductDetailResponse.cs backend/Ecommerce/Errors/ProductErrors.cs backend/Ecommerce/Services/IProductService.cs backend/Ecommerce/Services/ProductService.cs backend/Ecommerce.Tests/Services/ProductServiceTests.cs backend/Ecommerce.Tests/StubFileStorage.cs
git commit -m "Add product image gallery: add/delete images, admin detail response"
```

---

## Task 3: `AdminProductsController` + lock down the public `ProductsController`

**Files:**
- Create: `backend/Ecommerce/Controllers/AdminProductsController.cs`
- Modify: `backend/Ecommerce/Controllers/ProductsController.cs`
- Modify: `backend/Ecommerce.Tests/Authorization/ProductsControllerAuthorizationTests.cs`

**Interfaces:**
- Consumes: `IProductService` (`GetAdminPageAsync`, `GetAdminDetailAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `ToggleStatusAsync`, `AddImagesAsync`, `DeleteImageAsync`) and the `Ecommerce.Contracts.Products` records (Tasks 1–2); `PermissionKeys.ProductsView`/`PermissionKeys.ProductsManage` (already exist); `Ecommerce.Authorization.AdminAuthDefaults.Scheme`; `HasPermissionAttribute`.
- Produces: `GET|POST api/Admin/Products`, `GET|PUT|DELETE api/Admin/Products/{id}`, `PUT api/Admin/Products/{id}/toggleStatus`, `POST api/Admin/Products/{id}/images`, `DELETE api/Admin/Products/{id}/images/{imageId}` — Task 4's `ProductServices` calls all seven. All responses are `ApiResponse<T>`.
- Produces: the public `api/Products` surface reduced to `GET ""` and `GET "{slug}"`, both `ApiResponse<T>`-wrapped (unchanged from today).

- [ ] **Step 1: Write the admin controller**

```csharp
// backend/Ecommerce/Controllers/AdminProductsController.cs
using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Products;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Products")]
[ApiController]
public class AdminProductsController(IProductService productService) : ControllerBase
{
    private readonly IProductService _productService = productService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.ProductsView)]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _productService.GetAdminPageAsync(search, page, pageSize, cancellationToken);
        return Ok(new ApiResponse<ProductsPageResponse>(StatusCodes.Status200OK, "Products loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionKeys.ProductsView)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetAdminDetailAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found."));

        return Ok(new ApiResponse<AdminProductDetailResponse>(StatusCodes.Status200OK, "Product loaded.", result.Value));
    }

    [HttpPost("")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> CreateAsync([FromForm] ProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _productService.AddAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create product."));

        var response = new ApiResponse<ProductResponse>(StatusCodes.Status201Created, "Product created.", result.Value);
        return Created($"/api/Admin/Products/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromForm] ProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update product."));

        return Ok(new ApiResponse<ProductResponse>(StatusCodes.Status200OK, "Product updated.", result.Value));
    }

    [HttpPut("{id:long}/toggleStatus")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> ToggleStatusAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _productService.ToggleStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not toggle product status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Product status toggled."));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete product."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Product deleted."));
    }

    [HttpPost("{id:long}/images")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> AddImagesAsync([FromRoute] long id, [FromForm] List<IFormFile> imageFiles, CancellationToken cancellationToken)
    {
        var result = await _productService.AddImagesAsync(id, imageFiles, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not add images."));

        return Ok(new ApiResponse<IReadOnlyList<ProductImageResponse>>(StatusCodes.Status200OK, "Images added.", result.Value));
    }

    [HttpDelete("{id:long}/images/{imageId:long}")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> DeleteImageAsync([FromRoute] long id, [FromRoute] long imageId, CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteImageAsync(id, imageId, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete image."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Image deleted."));
    }
}
```

The multipart field name for the gallery upload is `imageFiles` (ASP.NET Core form-key matching is case-insensitive, so the frontend may send `imageFiles` or `ImageFiles` — Task 4 uses `imageFiles` to match this parameter name exactly).

- [ ] **Step 2: Reduce the public controller to its two read actions**

Replace the whole file:

```csharp
// backend/Ecommerce/Controllers/ProductsController.cs
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Products;

namespace Ecommerce.Controllers
{
    // Storefront-facing, unauthenticated, read-only.
    // Every write action now lives on AdminProductsController behind
    // AdminBearer + products.manage.
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController(IProductService productService) : ControllerBase
    {
        private readonly IProductService _productService = productService;

        [HttpGet("")]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var products = await _productService.GetAllAsync(cancellationToken);
            var response = new ApiResponse<IEnumerable<ProductResponse>>(StatusCodes.Status200OK, "", products.Adapt<IEnumerable<ProductResponse>>());
            return Ok(response);
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> Get([FromRoute] string slug, CancellationToken cancellationToken)
        {
            var result = await _productService.GetByIdOrSlugAsync(slug, cancellationToken);

            if (!result.IsSuccess)
            {
                var errorResponse = new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found.");
                return NotFound(errorResponse);
            }

            var response = new ApiResponse<ProductDetailsResponse>(StatusCodes.Status200OK, "Product retrieved successfully.", result.Value);
            return Ok(response);
        }
    }
}
```

- [ ] **Step 3: Fix the authorization test for the new controller split**

Replace the whole file:

```csharp
// backend/Ecommerce.Tests/Authorization/ProductsControllerAuthorizationTests.cs
using System.Reflection;
using Ecommerce.Authorization;
using Ecommerce.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Tests.Authorization;

public class ProductsControllerAuthorizationTests
{
    [Fact]
    public void AdminProductsController_requires_the_admin_bearer_scheme()
    {
        var classAuth = typeof(AdminProductsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).SingleOrDefault();

        Assert.NotNull(classAuth);
        Assert.Equal(AdminAuthDefaults.Scheme, classAuth!.AuthenticationSchemes);
    }

    [Theory]
    [InlineData("GetAllAsync", "ProductsView")]
    [InlineData("GetByIdAsync", "ProductsView")]
    [InlineData("CreateAsync", "ProductsManage")]
    [InlineData("UpdateAsync", "ProductsManage")]
    [InlineData("DeleteAsync", "ProductsManage")]
    [InlineData("ToggleStatusAsync", "ProductsManage")]
    [InlineData("AddImagesAsync", "ProductsManage")]
    [InlineData("DeleteImageAsync", "ProductsManage")]
    public void AdminProductsController_actions_require_the_expected_permission(string actionName, string permissionKeyName)
    {
        var action = typeof(AdminProductsController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;
        var permission = action.GetCustomAttributes<HasPermissionAttribute>(inherit: true).SingleOrDefault();
        var expectedKey = typeof(PermissionKeys).GetField(permissionKeyName, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!.ToString();

        Assert.NotNull(permission);
        Assert.Equal($"{AdminAuthDefaults.PolicyPrefix}{expectedKey}", permission!.Policy);
    }

    [Theory]
    [InlineData("GetAll")]
    [InlineData("Get")]
    public void Public_read_actions_stay_unauthenticated(string actionName)
    {
        var action = typeof(ProductsController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;

        Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Empty(typeof(ProductsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }
}
```

- [ ] **Step 4: Build and run the whole suite**

```powershell
dotnet build Ecommerce.slnx
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: 0 build errors, 127 tests passing. Running total: 106 (baseline) + 9 (`ProductServiceTests`, Task 1) + 7 (gallery tests appended in Task 2) = 122, then this task's `ProductsControllerAuthorizationTests` rewrite removes the old file's 6 test cases (4 `Write_actions_...` + 2 `Read_actions_...`) and adds 11 (1 `AdminProductsController_requires_the_admin_bearer_scheme` + 8 `AdminProductsController_actions_require_the_expected_permission` + 2 `Public_read_actions_stay_unauthenticated`) — net +5, giving 127.

- [ ] **Step 5: Manually verify the endpoints**

```powershell
dotnet run --project backend/Ecommerce
```

Log in as `admin.tester@example.com` / `AdminTester@123` via `POST https://localhost:7297/api/Admin/Auth/login` to get a token, then:

```bash
curl.exe -k https://localhost:7297/api/Products
curl.exe -k -X POST https://localhost:7297/api/Products -F "Title=Hacked" -F "Slug=hacked" -F "Sku=HACK-1" -F "Price=1" -F "CategoryId=1"
curl.exe -k "https://localhost:7297/api/Admin/Products?page=1&pageSize=5" -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Admin/Products
```

Expected, in order: `200` with the wrapped product list; **`405 Method Not Allowed`** (the public write action is gone); `200` with the paged admin list; `401 Unauthorized`.

Then create a product with a cover image and a gallery image:

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Products -H "Authorization: Bearer <token>" -F "CategoryId=1" -F "Title=Test Product" -F "Slug=test-product" -F "Sku=TEST-1" -F "Price=19.99" -F "StockQuantity=10" -F "ImageFile=@C:\path\to\cover.jpg"
```

Note the returned `id`, then:

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Products/<id>/images -H "Authorization: Bearer <token>" -F "imageFiles=@C:\path\to\gallery1.jpg" -F "imageFiles=@C:\path\to\gallery2.jpg"
curl.exe -k https://localhost:7297/api/Admin/Products/<id> -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Products/test-product
```

Expected: the gallery POST returns `200` with two `ProductImageResponse` entries; the admin detail GET shows both images ordered by `Sort`; the public detail GET's `images` array also shows both URLs (proving `GetByIdOrSlugAsync`'s existing gallery projection, dormant until now, is finally populated). Delete one via `DELETE https://localhost:7297/api/Admin/Products/<id>/images/<imageId>` and confirm the admin detail GET drops to one image.

- [ ] **Step 6: Commit**

```bash
git add backend/Ecommerce/Controllers/AdminProductsController.cs backend/Ecommerce/Controllers/ProductsController.cs backend/Ecommerce.Tests/Authorization/ProductsControllerAuthorizationTests.cs
git commit -m "Add AdminProductsController and make the public ProductsController read-only"
```

---

## Task 4: Products admin page

**Files:**
- Modify: `frontend/src/app/admin/shared/interface/productInterface.ts` (currently a dead pre-Phase-1 stub)
- Modify: `frontend/src/app/admin/core/services/product-services.ts` (currently a non-functional stub)
- Create: `frontend/src/app/admin/features/pages/products/products.ts`
- Create: `frontend/src/app/admin/features/pages/products/products.html`
- Create: `frontend/src/app/admin/features/pages/products/products.scss`
- Modify: `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts` (add a `NAV_ITEMS` entry)
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.routes.server.ts`

**Interfaces:**
- Consumes: `GET|POST api/Admin/Products`, `GET|PUT|DELETE api/Admin/Products/{id}`, `PUT api/Admin/Products/{id}/toggleStatus`, `POST api/Admin/Products/{id}/images`, `DELETE api/Admin/Products/{id}/images/{imageId}` (Task 3); `AdminApiEnvelope<T>` and `AdminAuthServices.hasPermission(key)` (Phase 1); `adminPermissionGuard('products.view')` (Phase 1); `CategoryServices.getCategories()` (admin, Phase 2B — reused unchanged for the category `<select>`).
- Produces: `AdminProductInterface`, `AdminProductDetailInterface`, `AdminProductImageInterface`, `ProductsPageInterface`, `ProductServices`, and the component class `Products` — `app.routes.ts` imports it as `Products as AdminProductsComponent` (the site tree already exports a `ProductsComponent`, same collision Categories hit).

- [ ] **Step 1: Write the interfaces**

```typescript
// frontend/src/app/admin/shared/interface/productInterface.ts
export interface AdminProductImageInterface {
  id: number;
  url: string;
  sort: number;
}

export interface AdminProductInterface {
  id: number;
  categoryId: number;
  title: string;
  slug: string;
  sku: string;
  price: number;
  priceAfterSale?: number | null;
  sale?: number | null;
  image?: string | null;
  stockQuantity: number;
  sort?: number | null;
  feature: boolean;
  status: boolean;
  metaDescription?: string | null;
  metaKey?: string | null;
}

export interface AdminProductDetailInterface {
  id: number;
  categoryId: number;
  title: string;
  slug: string;
  sku: string;
  price: number;
  priceAfterSale?: number | null;
  sale?: number | null;
  description?: string | null;
  image?: string | null;
  images: AdminProductImageInterface[];
  stockQuantity: number;
  sort?: number | null;
  feature: boolean;
  status: boolean;
  metaDescription?: string | null;
  metaKey?: string | null;
}

export interface ProductsPageInterface {
  items: AdminProductInterface[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
```

- [ ] **Step 2: Write `ProductServices`**

```typescript
// frontend/src/app/admin/core/services/product-services.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import {
  AdminProductDetailInterface,
  AdminProductImageInterface,
  AdminProductInterface,
  ProductsPageInterface,
} from '../../shared/interface/productInterface';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class ProductServices {
  private http = inject(HttpClient);

  getProducts(search: string, page: number, pageSize: number): Observable<ProductsPageInterface> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);

    return this.http.get<AdminApiEnvelope<ProductsPageInterface>>('/Admin/Products', { params }).pipe(map(response => response.data));
  }

  getProduct(id: number): Observable<AdminProductDetailInterface> {
    return this.http.get<AdminApiEnvelope<AdminProductDetailInterface>>(`/Admin/Products/${id}`).pipe(map(response => response.data));
  }

  // Products are posted as multipart/form-data because the request carries an
  // optional ImageFile. Do not set Content-Type — the browser adds the boundary.
  createProduct(payload: FormData): Observable<AdminProductInterface> {
    return this.http.post<AdminApiEnvelope<AdminProductInterface>>('/Admin/Products', payload).pipe(map(response => response.data));
  }

  updateProduct(id: number, payload: FormData): Observable<AdminProductInterface> {
    return this.http.put<AdminApiEnvelope<AdminProductInterface>>(`/Admin/Products/${id}`, payload).pipe(map(response => response.data));
  }

  toggleStatus(id: number): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Products/${id}/toggleStatus`, {}).pipe(map(() => undefined));
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Products/${id}`).pipe(map(() => undefined));
  }

  addImages(id: number, files: File[]): Observable<AdminProductImageInterface[]> {
    const payload = new FormData();
    files.forEach(file => payload.append('imageFiles', file, file.name));
    return this.http.post<AdminApiEnvelope<AdminProductImageInterface[]>>(`/Admin/Products/${id}/images`, payload).pipe(map(response => response.data));
  }

  deleteImage(id: number, imageId: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Products/${id}/images/${imageId}`).pipe(map(() => undefined));
  }
}
```

- [ ] **Step 3: Write the component**

```typescript
// frontend/src/app/admin/features/pages/products/products.ts
import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ProductServices } from '../../../core/services/product-services';
import { CategoryServices } from '../../../core/services/category-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { AdminProductDetailInterface, AdminProductInterface } from '../../../shared/interface/productInterface';
import { AdminCategoryInterface } from '../../../shared/interface/categoryInterface';
import { Environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-admin-products',
  imports: [ReactiveFormsModule, CurrencyPipe],
  templateUrl: './products.html',
  styleUrl: './products.scss',
})
export class Products {
  private productService = inject(ProductServices);
  private categoryService = inject(CategoryServices);
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  private readonly pageSize = 20;
  // Uploaded images are served by the API host, not the Angular dev server.
  private readonly assetOrigin = Environment.apiUrl.replace(/\/api\/?$/, '');

  products = signal<AdminProductInterface[]>([]);
  categories = signal<AdminCategoryInterface[]>([]);
  page = signal(1);
  totalPages = signal(0);
  totalCount = signal(0);
  searchTerm = signal('');

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  selectedFile = signal<File | null>(null);
  existingImage = signal<string | null>(null);

  editingDetail = signal<AdminProductDetailInterface | null>(null);
  galleryFiles = signal<File[]>([]);
  uploadingGallery = signal(false);
  deletingImageId = signal<number | null>(null);

  canManage = () => this.auth.hasPermission('products.manage');

  form = this.fb.nonNullable.group({
    categoryId: [0, Validators.required],
    title: ['', Validators.required],
    slug: ['', Validators.required],
    sku: ['', Validators.required],
    price: [0, [Validators.required, Validators.min(0.01)]],
    priceAfterSale: [0],
    sale: [0],
    stockQuantity: [0],
    sort: [0],
    description: [''],
    metaDescription: [''],
    metaKey: [''],
    status: [true],
    feature: [false],
  });

  constructor() {
    this.load();
    this.categoryService.getCategories().subscribe(categories => this.categories.set(categories));
  }

  private load(): void {
    this.loading.set(true);
    this.productService.getProducts(this.searchTerm(), this.page(), this.pageSize).subscribe({
      next: data => {
        this.products.set(data.items);
        this.totalPages.set(data.totalPages);
        this.totalCount.set(data.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  categoryTitle(categoryId: number): string {
    return this.categories().find(c => c.id === categoryId)?.title ?? '—';
  }

  imageUrl(path?: string | null): string {
    if (!path) return '';
    return /^https?:\/\//i.test(path) ? path : `${this.assetOrigin}${path}`;
  }

  onSearchInput(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  search(): void {
    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    if (page < 1 || (this.totalPages() > 0 && page > this.totalPages())) return;
    this.page.set(page);
    this.load();
  }

  startAdd(): void {
    this.editingId.set(null);
    this.editingDetail.set(null);
    this.selectedFile.set(null);
    this.existingImage.set(null);
    this.galleryFiles.set([]);
    this.form.reset({
      categoryId: this.categories()[0]?.id ?? 0,
      title: '',
      slug: '',
      sku: '',
      price: 0,
      priceAfterSale: 0,
      sale: 0,
      stockQuantity: 0,
      sort: 0,
      description: '',
      metaDescription: '',
      metaKey: '',
      status: true,
      feature: false,
    });
    this.showForm.set(true);
  }

  startEdit(product: AdminProductInterface): void {
    this.editingId.set(product.id);
    this.editingDetail.set(null);
    this.selectedFile.set(null);
    this.existingImage.set(product.image ?? null);
    this.galleryFiles.set([]);
    this.form.reset({
      categoryId: product.categoryId,
      title: product.title,
      slug: product.slug,
      sku: product.sku,
      price: product.price,
      priceAfterSale: product.priceAfterSale ?? 0,
      sale: product.sale ?? 0,
      stockQuantity: product.stockQuantity,
      sort: product.sort ?? 0,
      description: '',
      metaDescription: product.metaDescription ?? '',
      metaKey: product.metaKey ?? '',
      status: product.status,
      feature: product.feature,
    });
    this.showForm.set(true);

    // The list response has no description or gallery — load the full
    // detail separately once the form is open.
    this.productService.getProduct(product.id).subscribe(detail => {
      this.editingDetail.set(detail);
      this.form.patchValue({ description: detail.description ?? '' });
    });
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  onGalleryFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.galleryFiles.set(input.files ? Array.from(input.files) : []);
  }

  private buildFormData(): FormData {
    const raw = this.form.getRawValue();
    const payload = new FormData();

    payload.append('CategoryId', String(raw.categoryId));
    payload.append('Title', raw.title);
    payload.append('Slug', raw.slug);
    payload.append('Sku', raw.sku);
    payload.append('Price', String(raw.price));
    payload.append('Status', String(raw.status));
    payload.append('Feature', String(raw.feature));
    payload.append('StockQuantity', String(raw.stockQuantity ?? 0));
    payload.append('Sort', String(raw.sort ?? 0));

    if (raw.priceAfterSale) payload.append('PriceAfterSale', String(raw.priceAfterSale));
    if (raw.sale) payload.append('Sale', String(raw.sale));
    if (raw.description) payload.append('Description', raw.description);
    if (raw.metaDescription) payload.append('MetaDescription', raw.metaDescription);
    if (raw.metaKey) payload.append('MetaKey', raw.metaKey);

    const file = this.selectedFile();
    if (file) {
      payload.append('ImageFile', file, file.name);
    } else if (this.existingImage()) {
      // Sending the current path back is how "leave the image alone" is expressed.
      payload.append('Image', this.existingImage()!);
    }

    return payload;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const editingId = this.editingId();
    const payload = this.buildFormData();
    const request$ = editingId
      ? this.productService.updateProduct(editingId, payload)
      : this.productService.createProduct(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this product. Check the SKU and slug are unique and the image is a JPG/PNG/WebP under 2 MB.');
      },
    });
  }

  uploadGalleryImages(): void {
    const id = this.editingId();
    const files = this.galleryFiles();
    if (!id || files.length === 0) return;

    this.uploadingGallery.set(true);
    this.productService.addImages(id, files).subscribe({
      next: added => {
        this.editingDetail.update(detail => (detail ? { ...detail, images: [...detail.images, ...added] } : detail));
        this.galleryFiles.set([]);
        this.uploadingGallery.set(false);
      },
      error: () => this.uploadingGallery.set(false),
    });
  }

  removeGalleryImage(imageId: number): void {
    const id = this.editingId();
    if (!id) return;

    this.deletingImageId.set(imageId);
    this.productService.deleteImage(id, imageId).subscribe({
      next: () => {
        this.editingDetail.update(detail => (detail ? { ...detail, images: detail.images.filter(i => i.id !== imageId) } : detail));
        this.deletingImageId.set(null);
      },
      error: () => this.deletingImageId.set(null),
    });
  }

  toggleStatus(product: AdminProductInterface): void {
    this.busyId.set(product.id);
    this.productService.toggleStatus(product.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(product: AdminProductInterface): void {
    this.busyId.set(product.id);
    this.productService.deleteProduct(product.id).subscribe({
      next: () => {
        this.products.update(items => items.filter(p => p.id !== product.id));
        this.totalCount.update(count => count - 1);
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
```

- [ ] **Step 4: Write the template**

```html
<!-- frontend/src/app/admin/features/pages/products/products.html -->
<div class="panel-header">
  <div>
    <h1 class="page-title">Products</h1>
    <p class="page-subtitle">Catalog — {{ totalCount() }} total.</p>
  </div>
  @if (!showForm()) {
    <div class="header-actions">
      <div class="search-box">
        <input
          type="search"
          class="form-control"
          placeholder="Search title or SKU…"
          [value]="searchTerm()"
          (input)="onSearchInput($event)"
          (keyup.enter)="search()">
        <button type="button" class="toggle-btn" (click)="search()">Search</button>
      </div>
      @if (canManage()) {
        <button type="button" class="add-btn" (click)="startAdd()">+ Add Product</button>
      }
    </div>
  }
</div>

@if (loading()) {
  <div class="state-message">Loading products…</div>
} @else if (!showForm()) {
  <table class="data-table">
    <thead>
      <tr>
        <th>Image</th>
        <th>Title</th>
        <th>SKU</th>
        <th>Category</th>
        <th>Price</th>
        <th>Stock</th>
        <th>Status</th>
        @if (canManage()) { <th>Actions</th> }
      </tr>
    </thead>
    <tbody>
      @for (product of products(); track product.id) {
        <tr>
          <td>
            @if (product.image) {
              <img class="thumb" [src]="imageUrl(product.image)" [alt]="product.title">
            } @else {
              <span class="thumb thumb-empty">—</span>
            }
          </td>
          <td>{{ product.title }}</td>
          <td class="muted">{{ product.sku }}</td>
          <td>{{ categoryTitle(product.categoryId) }}</td>
          <td>{{ product.price | currency }}</td>
          <td>{{ product.stockQuantity }}</td>
          <td>
            <span class="pill" [class.pill-off]="!product.status">{{ product.status ? 'Active' : 'Hidden' }}</span>
          </td>
          @if (canManage()) {
            <td class="actions">
              <button type="button" (click)="startEdit(product)">Edit</button>
              <button type="button" [disabled]="busyId() === product.id" (click)="toggleStatus(product)">Toggle</button>
              <button type="button" class="danger" [disabled]="busyId() === product.id" (click)="remove(product)">Delete</button>
            </td>
          }
        </tr>
      } @empty {
        <tr><td colspan="8" class="state-message">No products match this search.</td></tr>
      }
    </tbody>
  </table>

  @if (totalPages() > 1) {
    <div class="pager">
      <button type="button" [disabled]="page() === 1" (click)="goToPage(page() - 1)">Previous</button>
      <span class="muted">Page {{ page() }} of {{ totalPages() }}</span>
      <button type="button" [disabled]="page() === totalPages()" (click)="goToPage(page() + 1)">Next</button>
    </div>
  }
}

@if (showForm()) {
  @if (error()) {
    <div class="alert-error">{{ error() }}</div>
  }

  <form [formGroup]="form" (ngSubmit)="save()" class="product-form">
    <div class="field-row">
      <div class="field-group">
        <label>Title</label>
        <input formControlName="title" type="text" class="form-control">
      </div>
      <div class="field-group">
        <label>Slug</label>
        <input formControlName="slug" type="text" class="form-control">
      </div>
    </div>

    <div class="field-row">
      <div class="field-group">
        <label>SKU</label>
        <input formControlName="sku" type="text" class="form-control">
      </div>
      <div class="field-group">
        <label>Category</label>
        <select formControlName="categoryId" class="form-control">
          @for (category of categories(); track category.id) {
            <option [value]="category.id">{{ category.title }}</option>
          }
        </select>
      </div>
    </div>

    <div class="field-row">
      <div class="field-group">
        <label>Price</label>
        <input formControlName="price" type="number" step="0.01" class="form-control">
      </div>
      <div class="field-group">
        <label>Price after sale</label>
        <input formControlName="priceAfterSale" type="number" step="0.01" class="form-control">
      </div>
      <div class="field-group">
        <label>Sale %</label>
        <input formControlName="sale" type="number" class="form-control">
      </div>
    </div>

    <div class="field-row">
      <div class="field-group">
        <label>Stock quantity</label>
        <input formControlName="stockQuantity" type="number" class="form-control">
      </div>
      <div class="field-group">
        <label>Sort</label>
        <input formControlName="sort" type="number" class="form-control">
      </div>
    </div>

    <div class="field-group">
      <label>Description</label>
      <textarea formControlName="description" rows="3" class="form-control"></textarea>
    </div>

    <div class="field-group">
      <label>Cover image</label>
      @if (existingImage()) {
        <img class="preview" [src]="imageUrl(existingImage())" alt="Current image">
      }
      <input type="file" accept="image/*" class="form-control" (change)="onFileSelected($event)">
      <small class="muted">JPG, PNG or WebP, up to 2 MB. Leave empty to keep the current image.</small>
    </div>

    @if (editingId()) {
      <div class="field-group">
        <label>Gallery</label>
        @if (editingDetail()?.images?.length) {
          <div class="gallery-strip">
            @for (image of editingDetail()!.images; track image.id) {
              <div class="gallery-thumb">
                <img [src]="imageUrl(image.url)" alt="Gallery image">
                <button type="button" class="danger" [disabled]="deletingImageId() === image.id" (click)="removeGalleryImage(image.id)">Remove</button>
              </div>
            }
          </div>
        } @else {
          <p class="muted">No gallery images yet.</p>
        }
        <input type="file" accept="image/*" multiple class="form-control" (change)="onGalleryFilesSelected($event)">
        <button type="button" class="toggle-btn" [disabled]="uploadingGallery() || !galleryFiles().length" (click)="uploadGalleryImages()">
          {{ uploadingGallery() ? 'Uploading…' : 'Add to gallery' }}
        </button>
      </div>
    } @else {
      <p class="muted">Save the product first to add gallery images.</p>
    }

    <div class="field-row">
      <div class="field-group">
        <label>Meta key</label>
        <input formControlName="metaKey" type="text" class="form-control">
      </div>
      <div class="field-group">
        <label>Meta description</label>
        <input formControlName="metaDescription" type="text" class="form-control">
      </div>
    </div>

    <div class="checkbox-row">
      <label class="checkbox-field"><input formControlName="feature" type="checkbox"> Featured</label>
      <label class="checkbox-field"><input formControlName="status" type="checkbox"> Active</label>
    </div>

    <div class="form-actions">
      <button type="submit" class="save-btn" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save Product' }}</button>
      <button type="button" class="cancel-btn" (click)="cancel()">Cancel</button>
    </div>
  </form>
}
```

- [ ] **Step 5: Write the styles**

```scss
// frontend/src/app/admin/features/pages/products/products.scss
@import '../../../shared/scss/variables';

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1.5rem;
  gap: 1rem;
}

.header-actions {
  display: flex;
  gap: 0.5rem;
}

.page-title {
  font-weight: 800;
  color: $admin-text;
  margin-bottom: 0.25rem;
}

.page-subtitle {
  color: $admin-muted;
  margin: 0;
}

.search-box {
  display: flex;
  gap: 0.5rem;

  .form-control { min-width: 220px; }
}

.add-btn, .save-btn {
  background: $admin-green;
  color: #fff;
  border: none;
  border-radius: 10px;
  padding: 0.65rem 1.1rem;
  font-weight: 700;
  cursor: pointer;

  &:hover { background: $admin-green-hover; }
  &:disabled { opacity: 0.6; cursor: not-allowed; }
}

.toggle-btn, .cancel-btn {
  background: transparent;
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.65rem 1.1rem;
  font-weight: 600;
  cursor: pointer;

  &:disabled { opacity: 0.5; cursor: not-allowed; }
}

.data-table {
  width: 100%;
  border-collapse: collapse;
  background: #fff;
  border-radius: $admin-radius;
  overflow: hidden;

  th, td {
    text-align: left;
    padding: 0.85rem 1rem;
    border-bottom: 1px solid rgba(0, 0, 0, 0.06);
    vertical-align: middle;
  }

  th {
    color: $admin-muted;
    font-size: 0.8rem;
    text-transform: uppercase;
  }
}

.thumb {
  width: 44px;
  height: 44px;
  object-fit: cover;
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.04);
}

.thumb-empty {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  color: $admin-muted;
}

.preview {
  width: 160px;
  height: 160px;
  object-fit: cover;
  border-radius: 10px;
  margin-bottom: 0.5rem;
}

.gallery-strip {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.gallery-thumb {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.35rem;

  img {
    width: 90px;
    height: 90px;
    object-fit: cover;
    border-radius: 8px;
  }

  button {
    border: none;
    background: transparent;
    color: #b3261e;
    font-weight: 600;
    font-size: 0.75rem;
    cursor: pointer;

    &:disabled { opacity: 0.4; cursor: not-allowed; }
  }
}

.muted {
  color: $admin-muted;
  font-size: 0.8rem;
}

.pill {
  background: rgba($admin-green, 0.12);
  color: $admin-green;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 700;

  &.pill-off {
    background: rgba(#b3261e, 0.1);
    color: #b3261e;
  }
}

.actions button {
  margin-right: 0.5rem;
  border: none;
  background: transparent;
  cursor: pointer;
  font-weight: 600;

  &.danger { color: #b3261e; }
  &:disabled { opacity: 0.4; cursor: not-allowed; }
}

.pager {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-top: 1rem;

  button {
    border: 1px solid rgba(0, 0, 0, 0.12);
    background: #fff;
    border-radius: 10px;
    padding: 0.4rem 0.9rem;
    font-weight: 600;
    cursor: pointer;

    &:disabled { opacity: 0.4; cursor: not-allowed; }
  }
}

.state-message {
  color: $admin-muted;
  padding: 1rem;
}

.alert-error {
  background: rgba(#b3261e, 0.08);
  color: #b3261e;
  padding: 0.6rem 0.75rem;
  border-radius: 10px;
  font-size: 0.85rem;
  margin-bottom: 1rem;
}

.product-form {
  background: #fff;
  border-radius: $admin-radius;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 720px;
}

.field-row {
  display: flex;
  gap: 1rem;

  .field-group { flex: 1; }
}

.field-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;

  label { font-weight: 600; font-size: 0.85rem; color: $admin-text; }
}

.form-control {
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.6rem 0.75rem;
}

.checkbox-row {
  display: flex;
  gap: 1.25rem;
}

.checkbox-field {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
}

.form-actions {
  display: flex;
  gap: 0.75rem;
}
```

- [ ] **Step 6: Add the sidebar nav entry**

In `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts`, add one entry to `NAV_ITEMS` after Categories (grouping the two catalog pages together). The finished array should read:

```typescript
const NAV_ITEMS: AdminNavItem[] = [
  { label: 'Dashboard', path: '.', icon: 'bi-grid-1x2-fill', permission: 'dashboard.view' },
  { label: 'Categories', path: 'categories', icon: 'bi-diagram-3-fill', permission: 'categories.view' },
  { label: 'Products', path: 'products', icon: 'bi-box-seam-fill', permission: 'products.view' },
  { label: 'Clients', path: 'clients', icon: 'bi-person-lines-fill', permission: 'clients.view' },
  { label: 'Sliders', path: 'sliders', icon: 'bi-images', permission: 'sliders.view' },
  { label: 'Roles', path: 'roles', icon: 'bi-shield-lock-fill', permission: 'roles.manage' },
  { label: 'Admins', path: 'admins', icon: 'bi-people-fill', permission: 'admins.manage' },
];
```

- [ ] **Step 7: Add the route**

In `frontend/src/app/app.routes.ts`, add the import (the site tree already exports a `ProductsComponent`, so alias this one):

```typescript
import { Products as AdminProductsComponent } from './admin/features/pages/products/products';
```

and add the child route inside the `path: 'admin'` block, after the categories entry:

```typescript
        { path: 'products', component: AdminProductsComponent, canActivate: [adminPermissionGuard('products.view')], title: 'Products' },
```

- [ ] **Step 8: Add the server render mode**

In `frontend/src/app/app.routes.server.ts`, add alongside the other admin entries (after `admin/categories`, before `admin/clients`):

```typescript
  {
    path: 'admin/products',
    renderMode: RenderMode.Client
  },
```

- [ ] **Step 9: Type-check**

```powershell
npx tsc --noEmit -p frontend/tsconfig.app.json
```

Expected: 0 errors.

- [ ] **Step 10: Manually verify**

With the backend and frontend running, logged in at `http://127.0.0.1:4200/admin/auth/login` as `admin.tester@example.com` / `AdminTester@123`:

1. A **Products** item appears in the sidebar between Categories and Clients; clicking it opens `/admin/products` showing the seeded products in a table with real thumbnails, correct category names, price, and stock.
2. Search for part of a product's title or SKU and press Enter — the list narrows; clear it and search again to get the full list back.
3. Click **+ Add Product**, fill in Title, Slug, SKU, pick a category, Price, Stock quantity, pick a cover image, save. The new row appears with a visible thumbnail and the stock value you entered.
4. Click **Edit** on that product — the form pre-fills, and a "Gallery" section with a file picker appears (since the product now has an id). Pick two images and click **Add to gallery** — both appear as thumbnails with a **Remove** button each. Remove one and confirm it disappears.
5. Open `https://localhost:7297/api/Products/<slug>` in a browser and confirm `images` now contains one URL (proving the storefront's `ProductDetailsComponent` gallery, dormant since Task 1 of this session's dummy-data fill, is finally populated for a product with real gallery photos) — then visit `http://127.0.0.1:4200/products/<slug>` and confirm the thumbnail row renders under the main image.
6. Click **Edit** again, change only the Stock quantity, save — the cover image is unchanged (the `Image` path round-tripped) and the table shows the new stock number.
7. Click **Toggle** — the Status pill flips to `Hidden`; toggle it back.
8. Delete the test product.
9. Create a role with **only** `products.view` (via `/admin/roles`), assign it to a second admin if feasible, or simulate it by removing `products.manage` from the stored session in the browser console (`JSON.parse`/re-`stringify` `localStorage.shopdemo_admin_auth`, same technique used for Categories/Clients/Sliders in this session) and reloading `/admin/products`: the page loads read-only — no `+ Add Product`, no Actions column.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/app/admin/shared/interface/productInterface.ts frontend/src/app/admin/core/services/product-services.ts frontend/src/app/admin/features/pages/products frontend/src/app/admin/features/layouts/main-layout/main-layout.ts frontend/src/app/app.routes.ts frontend/src/app/app.routes.server.ts
git commit -m "Add Products admin page with search, pagination, and image gallery"
```

---

## Plan-level final check

Once all 4 tasks are done:

- [ ] `dotnet test backend/Ecommerce.Tests/Ecommerce.Tests.csproj` — all passing, including the 16 `ProductServiceTests` and the retargeted `ProductsControllerAuthorizationTests`, plus everything Phase 1, Plan 2A, and Phase 2B contributed.
- [ ] `dotnet build backend/Ecommerce.slnx` — 0 errors.
- [ ] `npx tsc --noEmit -p frontend/tsconfig.app.json` — 0 errors.
- [ ] `npm run build` from `frontend/` — completes. The prerender step must not attempt to render `/admin/products` (`RenderMode.Client`). Watch the bundle-budget warning (raised to 1.5 MB error / 800 kB warning in Task 10 of Phase 2B) — if a fourth admin page pushes it into error territory, raise the budget again rather than trimming functionality, same call made last time.
- [ ] **Design-doc coverage sweep.** Confirm in the running app: `AdminProductsController` at `api/Admin/Products` gated `products.view`/`products.manage`; cover image upload through `IFileStorage` module `"products"`; gallery add/delete through the same module, backed by the now-live `ProductImage` table; `StockQuantity`/`Status`/`Feature` all editable and no longer hardcoded on create; public `ProductsController` reduced to `GET ""`/`GET "{slug}"`; admin UI has search + pagination + a gallery section gated behind the product already existing.
- [ ] **Full manual walkthrough:** admin login → Products (search, create with cover image, edit, add/remove gallery images, toggle, delete) → confirm the storefront `/products` grid and `/products/:slug` detail page still render correctly (unchanged reads, now with real gallery data for any product that has one) → confirm a `products.view`-only session renders the page read-only.
- [ ] **Storefront regression check:** `/home`, `/categories`, `/products`, `/products/:slug`, `/cart`, `/checkout` all still work — the two files this plan touches that customer flows also depend on are `ProductsController` (writes removed, reads unchanged) and `ProductService.GetByIdOrSlugAsync` (unchanged, but its gallery projection now actually returns data).
- [ ] Confirm nothing in this plan set `CreatedById`/`UpdatedById`/`IsDeleted` by hand or threaded an `adminId` through a service:

  ```powershell
  rg -n "IsDeleted\s*=|CreatedById\s*=|UpdatedById\s*=|DeletedById\s*=" backend/Ecommerce/Services backend/Ecommerce/Controllers
  ```

  Expected: no matches.
