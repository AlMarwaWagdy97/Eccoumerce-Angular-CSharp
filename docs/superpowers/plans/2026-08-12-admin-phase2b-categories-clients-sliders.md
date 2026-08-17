# Admin Dashboard Phase 2B: Categories, Clients & Sliders Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the three remaining non-Products/Orders/Dashboard admin features from the Phase 2 design — Categories management (hierarchical), Clients management (customer accounts), and Sliders (green-field entity plus its public storefront endpoint) — on top of the audit/soft-delete and file-upload foundations delivered by Plan 2A.

**Architecture:** Each admin feature gets its **own admin controller** under `api/Admin/...` on the `AdminBearer` scheme, gated per-action with `[HasPermission(PermissionKeys.X)]`. Categories reuses the existing `ICategoryService`/`CategoryService` (same entity, same rules) and only gains `IFileStorage` handling; the public `CategoriesController` is reduced to its two read actions, closing the unauthenticated-write hole. Clients gets a brand-new `IClientService`/`ClientService` over `ApplicationUser`, using ASP.NET Identity's built-in lockout for enable/disable and `UserManager` for every email mutation. Sliders is green-field: a new `Slider : AuditableEntity`, a new `ISliderService`/`SliderService`, an `AdminSlidersController`, and a public read-only `SlidersController` that evaluates the `StartsOn`/`EndsOn` schedule server-side. Frontend mirrors the Phase 1 Roles/Admins pages: signal-based standalone components, `XServices` HTTP services, `adminPermissionGuard('<key>')` on the route, and write controls additionally hidden when the admin lacks the matching `.manage` permission.

**Note on migrations — deliberate deviation from the design doc.** The design doc says `Slider` ships in the same EF migration as the audit columns. Because Phase 2 is split into two plans, that is **not** what happens: Plan 2A owns migration `AddAuditAndSoftDelete`, covering only the pre-existing entities. **`Slider` gets its own migration, `AddSliders`, in this plan (Task 7).** Since 2A has already added the audit columns to every `IAuditable` entity by then, `AddSliders` creates the `Sliders` table *including* its audit columns and `Admin` FKs in one go.

**Tech Stack:** ASP.NET Core .NET 10, EF Core (SQL Server), FluentValidation, Mapster, xUnit + Moq + EF Core InMemory. Angular 22 standalone components, signals, SCSS.

## Global Constraints

- **Plan 2A's deliverables already exist. Do not re-implement them.** Specifically:
  - `Ecommerce.Entities.IAuditable` and `Ecommerce.Entities.AuditableEntity` (with `long? CreatedById`, `DateTime CreatedOn`, `long? UpdatedById`, `DateTime? UpdatedOn`, `bool IsDeleted`, `DateTime? DeletedOn`, `long? DeletedById`, and nav props `Admin? CreatedBy`, `Admin? UpdatedBy`, `Admin? DeletedBy`). `Category`, `Product`, `ProductImage`, `Order`, `OrderItem`, `Address`, `Card`, `Review`, `Admin`, `AdminRole` inherit `AuditableEntity`; `ApplicationUser` implements `IAuditable` directly.
  - **Audit stamping and soft-delete are fully automatic inside `ApplicationDbContext.SaveChangesAsync`/`SaveChanges`.** Services in this plan must **not** take an `adminId` parameter, must **not** set `CreatedById`/`UpdatedById`/`IsDeleted`/`DeletedOn` by hand, and a plain `_context.Remove(entity)` **already becomes a soft delete**.
  - A **global EF query filter `!IsDeleted`** is applied to every `IAuditable` entity, so ordinary queries already exclude soft-deleted rows. Use `.IgnoreQueryFilters()` only where deleted rows are deliberately wanted (this plan uses it in exactly one place — the client email-uniqueness pre-check).
  - `Ecommerce.Storage.IFileStorage`, registered Scoped as `LocalFileStorage`:
    ```csharp
    public interface IFileStorage
    {
        Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken cancellationToken = default);
    }
    ```
    On success `Result.Value` is the stored public relative path (e.g. `/uploads/categories/3f2b….jpg`) which is what gets persisted into the entity's `Image` string column. On failure it returns `Result.Failure<string>` with `Ecommerce.Errors.FileErrors.EmptyFile` (`"File.Empty"`), `FileErrors.UnsupportedType` (`"File.UnsupportedType"`), or `FileErrors.TooLarge` (`"File.TooLarge"`). `app.UseStaticFiles()` and `wwwroot/uploads/` already exist.
  - `ProductsController`'s write actions are already permission-gated by Plan 2A. **Do not touch them.**
- Follow `backend/CLAUDE.md` conventions: thin controllers → `Scoped` services returning `Result`/`Result<T>`, `ApiResponse<T>` envelope, DTOs as `record`s with FluentValidation validators, per-domain `*Errors` classes, primary-constructor DI, `.AsNoTracking()` on reads, trailing `CancellationToken` on all service methods.
- Follow `frontend/CLAUDE.md` conventions: Angular 22 bare file naming (no `.component.ts`), standalone components with explicit `imports`, `@if`/`@for` control flow, signals, services named `XServices`, external templates/styles.
- **`Result.IsFailure` is `internal`** and therefore not visible from `Ecommerce.Tests`. Always assert on `Assert.True(result.IsSuccess)` / `Assert.False(result.IsSuccess)`, never `IsFailure`.
- Backend tasks are TDD: write the failing test, run it and watch it fail, implement, run it and watch it pass, commit. Test commands run from `backend/`:
  ```powershell
  dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter <TestClassName>
  ```
- **Frontend tasks use manual browser verification, not Vitest specs.** The Phase 1 plan established this explicitly ("frontend has no established component/service test convention; do not add frontend unit tests as part of this plan"). Every frontend task ends with a concrete click-through using the seeded admin `admin.tester@example.com` / `AdminTester@123`.
- Running the stack: backend `dotnet run --project backend/Ecommerce` (https://localhost:7297), frontend `npm start` from `frontend/` (http://localhost:4200).
- Uploaded images are served by the **API** host, not the Angular dev server. The stored path is API-relative (`/uploads/...`), so every admin template that renders one must prefix it with the API origin derived from `Environment.apiUrl`. Each frontend task that shows an image includes the same `imageUrl()` helper — copy it verbatim.
- **Three frontend tasks (3, 6, 10) each edit `frontend/src/app/app.routes.ts`, `frontend/src/app/app.routes.server.ts`, and `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts`.** Run them sequentially, not in parallel, and re-read those three files before editing.
- No entity, contract, DTO, service-method, or component name introduced in one task may be renamed in a later task — check the **Interfaces** block of each task before writing code that depends on an earlier task.

---

## Task 1: Category image upload in `CategoryService`

**Files:**
- Modify: `backend/Ecommerce/Contracts/Categories/CategoryRequest.cs`
- Modify: `backend/Ecommerce/Errors/CategoryErrors.cs`
- Modify: `backend/Ecommerce/Services/CategoryService.cs`
- Create: `backend/Ecommerce.Tests/StubFileStorage.cs`
- Test: `backend/Ecommerce.Tests/Services/CategoryServiceTests.cs`

**Interfaces:**
- Consumes: `Ecommerce.Storage.IFileStorage.SaveAsync(IFormFile file, string module, CancellationToken)` → `Task<Result<string>>` (Plan 2A); `Ecommerce.Errors.FileErrors.UnsupportedType` (Plan 2A).
- Produces: `CategoryRequest(long? ParentId, string Title, string Slug, string? Description, string? Image, int? Sort, string? MetaDescription, string? MetaKey, bool? Feature = false, bool? Status = true, IFormFile? ImageFile = null)` — Task 2's `AdminCategoriesController` binds this with `[FromForm]`, and Task 3's frontend posts a `FormData` whose field names match these property names.
- Produces: `CategoryErrors.InvalidParent` (code `"Category.InvalidParent"`).
- Produces: `CategoryService(ApplicationDbContext context, IFileStorage fileStorage)` — the constructor gains a second parameter; the DI registration in `DependacyInjection.cs` already reads `services.AddScoped<ICategoryService, CategoryService>()` and needs no change.
- Produces: `Ecommerce.Tests.StubFileStorage` and `Ecommerce.Tests.TestFiles.Image(...)` — Task 8's `SliderServiceTests` reuses both.
- `ICategoryService`'s method signatures are **unchanged**: `GetAllAsync`, `GetAsync(long)`, `AddAsync(CategoryRequest)`, `UpdateAsync(long, CategoryRequest)`, `DeleteAsync(long)`, `ToggleStatusAsync(long)`.

- [ ] **Step 1: Add `ImageFile` to `CategoryRequest`**

Replace the whole file:

```csharp
// backend/Ecommerce/Contracts/Categories/CategoryRequest.cs
namespace Ecommerce.Contracts.Categories
{
    // ImageFile is the multipart upload; Image is the already-stored path.
    // If ImageFile is present it wins and its saved path replaces Image;
    // otherwise Image is kept as-is, which is how "leave the current image
    // alone" is expressed on an update.
    public record class CategoryRequest(
        long? ParentId,
        string Title,
        string Slug,
        string? Description,
        string? Image,
        int? Sort,
        string? MetaDescription,
        string? MetaKey,
        bool? Feature = false,
        bool? Status = true,
        IFormFile? ImageFile = null
    );

    public class CategoryRequestValidator : AbstractValidator<CategoryRequest>
    {
        public CategoryRequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.MetaDescription).MaximumLength(500);
            RuleFor(x => x.MetaKey).MaximumLength(255);
        }
    }
}
```

(The pre-existing `CategoryRequestValidation : AbstractValidator<Category>` in the sibling file validates the *entity*, not this record, and no action binds a `Category` — leave that file alone.)

- [ ] **Step 2: Add the `InvalidParent` error**

```csharp
// backend/Ecommerce/Errors/CategoryErrors.cs
namespace Ecommerce.Errors
{
    public class CategoryErrors
    {
        public static readonly Error CategoryNotFound = new("404", "No category was found with the given ID");
        public static readonly Error DuplicatedCategorySlug = new("Category.DuplicatedSlug", "Another category with the same slug already exists");
        public static readonly Error InvalidParent = new("Category.InvalidParent", "A category cannot be its own parent");
    }
}
```

- [ ] **Step 3: Add the shared `IFileStorage` test double**

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
    private readonly string _savedPath = savedPath;
    private readonly Error? _failWith = failWith;

    public string? LastModule { get; private set; }
    public int SaveCallCount { get; private set; }

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

- [ ] **Step 4: Write the failing tests**

```csharp
// backend/Ecommerce.Tests/Services/CategoryServiceTests.cs
using Ecommerce.Contracts.Categories;
using Ecommerce.Entities;
using Ecommerce.Errors;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class CategoryServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static CategoryRequest Request(
        string title = "Shoes",
        string slug = "shoes",
        string? image = null,
        long? parentId = null,
        IFormFile? imageFile = null) =>
        new(parentId, title, slug, null, image, 1, null, null, false, true, imageFile);

    [Fact]
    public async Task AddAsync_saves_the_uploaded_image_and_uses_its_path()
    {
        await using var context = CreateContext();
        var storage = new StubFileStorage("/uploads/categories/abc.jpg");
        var service = new CategoryService(context, storage);

        var result = await service.AddAsync(Request(imageFile: TestFiles.Image()));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/categories/abc.jpg", result.Value.Image);
        Assert.Equal("categories", storage.LastModule);
    }

    [Fact]
    public async Task AddAsync_keeps_the_supplied_image_string_when_no_file_is_uploaded()
    {
        await using var context = CreateContext();
        var storage = new StubFileStorage();
        var service = new CategoryService(context, storage);

        var result = await service.AddAsync(Request(image: "/uploads/categories/seeded.png"));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/categories/seeded.png", result.Value.Image);
        Assert.Equal(0, storage.SaveCallCount);
    }

    [Fact]
    public async Task AddAsync_fails_for_a_duplicate_slug()
    {
        await using var context = CreateContext();
        var service = new CategoryService(context, new StubFileStorage());
        await service.AddAsync(Request(slug: "shoes"));

        var result = await service.AddAsync(Request(title: "Other shoes", slug: "shoes"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Category.DuplicatedSlug", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_propagates_a_file_storage_failure()
    {
        await using var context = CreateContext();
        var storage = new StubFileStorage(failWith: FileErrors.UnsupportedType);
        var service = new CategoryService(context, storage);

        var result = await service.AddAsync(Request(imageFile: TestFiles.Image("virus.exe")));

        Assert.False(result.IsSuccess);
        Assert.Equal("File.UnsupportedType", result.Error.Code);
        Assert.False(await context.Categories.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_keeps_the_existing_image_when_neither_a_file_nor_an_image_path_is_supplied()
    {
        await using var context = CreateContext();
        var service = new CategoryService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request(image: "/uploads/categories/original.jpg"))).Value;

        var result = await service.UpdateAsync(created.Id, Request(title: "Renamed", slug: "renamed"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value.Title);
        Assert.Equal("/uploads/categories/original.jpg", result.Value.Image);
    }

    [Fact]
    public async Task UpdateAsync_fails_when_a_category_is_made_its_own_parent()
    {
        await using var context = CreateContext();
        var service = new CategoryService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request())).Value;

        var result = await service.UpdateAsync(created.Id, Request(parentId: created.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal("Category.InvalidParent", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_category_from_ordinary_queries()
    {
        await using var context = CreateContext();
        var service = new CategoryService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request())).Value;

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Categories.AnyAsync(x => x.Id == created.Id));
    }
}
```

- [ ] **Step 5: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter CategoryServiceTests
```

Expected: FAIL — `CategoryService` still has a one-parameter constructor and `CategoryErrors.InvalidParent` / `StubFileStorage` are new.

- [ ] **Step 6: Rewrite `CategoryService`**

Replace the whole file. Note three deliberate changes beyond the upload handling: `FindAsync` becomes `FirstOrDefaultAsync` so the soft-delete query filter applies; `AddAsync` now honours the request's `Status`/`Feature` instead of hard-coding them (the admin UI needs to create a hidden category); and mapping is written out explicitly rather than via `Adapt`, so `ImageFile` and the inherited audit properties can never be clobbered.

```csharp
// backend/Ecommerce/Services/CategoryService.cs
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
```

- [ ] **Step 7: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter CategoryServiceTests
```

Expected: 7 passed.

- [ ] **Step 8: Build the solution**

```powershell
dotnet build Ecommerce.slnx
```

Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add backend/Ecommerce/Contracts/Categories/CategoryRequest.cs backend/Ecommerce/Errors/CategoryErrors.cs backend/Ecommerce/Services/CategoryService.cs backend/Ecommerce.Tests/StubFileStorage.cs backend/Ecommerce.Tests/Services/CategoryServiceTests.cs
git commit -m "Add image upload and parent validation to CategoryService"
```

---

## Task 2: `AdminCategoriesController` + lock down the public `CategoriesController`

**Files:**
- Create: `backend/Ecommerce/Controllers/AdminCategoriesController.cs`
- Modify: `backend/Ecommerce/Controllers/CategoriesController.cs`

**Interfaces:**
- Consumes: `ICategoryService` (`GetAllAsync`, `GetAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `ToggleStatusAsync`) and `CategoryRequest` with `IFormFile? ImageFile` (Task 1); `PermissionKeys.CategoriesView` / `PermissionKeys.CategoriesManage`; `Ecommerce.Authorization.AdminAuthDefaults.Scheme`; `HasPermissionAttribute`.
- Produces: `GET|POST api/Admin/Categories`, `GET|PUT|DELETE api/Admin/Categories/{id}`, `PUT api/Admin/Categories/{id}/toggleStatus` — Task 3's `CategoryServices` calls all six. All responses are `ApiResponse<T>`; the list/detail payload is `CategoryResponse { Id, ParentId, Title, Slug, Description, Image, Sort, Feature, Status, MetaDescription, MetaKey }`.
- Produces: the public `api/Categories` surface reduced to `GET ""` and `GET "{id}"`, both `ApiResponse<T>`-wrapped.

- [ ] **Step 1: Write the admin controller**

Mirrors `RolesController`'s attribute usage exactly: class-level `[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]` plus a per-action `[HasPermission(...)]`, because reads and writes need different keys.

```csharp
// backend/Ecommerce/Controllers/AdminCategoriesController.cs
using Ecommerce.Authorization;
using Ecommerce.Contracts.Categories;
using Ecommerce.Contracts.Common;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Categories")]
[ApiController]
public class AdminCategoriesController(ICategoryService categoryService) : ControllerBase
{
    private readonly ICategoryService _categoryService = categoryService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.CategoriesView)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var categories = await _categoryService.GetAllAsync(cancellationToken);
        var response = new ApiResponse<IEnumerable<CategoryResponse>>(
            StatusCodes.Status200OK, "Categories loaded.", categories.Adapt<IEnumerable<CategoryResponse>>());

        return Ok(response);
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionKeys.CategoriesView)]
    public async Task<IActionResult> GetAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.GetAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Category not found."));

        return Ok(new ApiResponse<CategoryResponse>(StatusCodes.Status200OK, "Category loaded.", result.Value));
    }

    [HttpPost("")]
    [HasPermission(PermissionKeys.CategoriesManage)]
    public async Task<IActionResult> AddAsync([FromForm] CategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.AddAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create category."));

        var response = new ApiResponse<CategoryResponse>(StatusCodes.Status201Created, "Category created.", result.Value);
        return Created($"/api/Admin/Categories/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionKeys.CategoriesManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromForm] CategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update category."));

        return Ok(new ApiResponse<CategoryResponse>(StatusCodes.Status200OK, "Category updated.", result.Value));
    }

    [HttpPut("{id:long}/toggleStatus")]
    [HasPermission(PermissionKeys.CategoriesManage)]
    public async Task<IActionResult> ToggleStatusAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.ToggleStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not toggle category status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Category status toggled."));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionKeys.CategoriesManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete category."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Category deleted."));
    }
}
```

- [ ] **Step 2: Reduce the public controller to its two read actions**

Replace the whole file. This deletes the unauthenticated `POST`/`PUT`/`DELETE`/`toggleStatus` actions (the hole this phase closes), drops the stray `using static System.Runtime.InteropServices.JavaScript.JSType;`, and makes both remaining actions return a consistent `ApiResponse<T>`.

```csharp
// backend/Ecommerce/Controllers/CategoriesController.cs
using Ecommerce.Contracts.Categories;
using Ecommerce.Contracts.Common;

namespace Ecommerce.Controllers
{
    // Storefront-facing, unauthenticated, read-only.
    // Every write action now lives on AdminCategoriesController behind
    // AdminBearer + categories.manage.
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController(ICategoryService categoryService) : ControllerBase
    {
        private readonly ICategoryService _categoryService = categoryService;

        [HttpGet("")]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var categories = await _categoryService.GetAllAsync(cancellationToken);
            var response = new ApiResponse<IEnumerable<CategoryResponse>>(
                StatusCodes.Status200OK, "", categories.Adapt<IEnumerable<CategoryResponse>>());

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get([FromRoute] long id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetAsync(id, cancellationToken);
            if (!result.IsSuccess)
                return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Category not found."));

            return Ok(new ApiResponse<CategoryResponse>(StatusCodes.Status200OK, "Category retrieved successfully.", result.Value));
        }
    }
}
```

- [ ] **Step 3: Confirm no storefront code called the removed write actions**

```powershell
rg -n "Categories" frontend/src/app/site --glob "*.ts"
```

Expected: only `GET` calls (`getCategories`, `getCategoryById` style). If any `post`/`put`/`delete` against `/Categories` shows up, stop and report it — nothing in this plan should break the storefront.

- [ ] **Step 4: Build and run the whole backend suite**

```powershell
dotnet build Ecommerce.slnx
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: 0 build errors, all tests passing.

- [ ] **Step 5: Manually verify the endpoints**

```powershell
dotnet run --project backend/Ecommerce
```

Log in as `admin.tester@example.com` / `AdminTester@123` via `POST https://localhost:7297/api/Admin/Auth/login` to get a token, then:

```bash
curl.exe -k https://localhost:7297/api/Categories
curl.exe -k -X POST https://localhost:7297/api/Categories -F "Title=Hacked" -F "Slug=hacked"
curl.exe -k https://localhost:7297/api/Admin/Categories -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Admin/Categories
```

Expected, in order: `200` with the wrapped category list; **`405 Method Not Allowed`** (the public write action is gone); `200` with the admin list; `401 Unauthorized`.

- [ ] **Step 6: Commit**

```bash
git add backend/Ecommerce/Controllers/AdminCategoriesController.cs backend/Ecommerce/Controllers/CategoriesController.cs
git commit -m "Add AdminCategoriesController and make the public CategoriesController read-only"
```

---

## Task 3: Categories admin page

**Files:**
- Modify: `frontend/src/app/admin/shared/interface/categoryInterface.ts` (currently a dead pre-Phase-1 stub)
- Modify: `frontend/src/app/admin/core/services/category-services.ts` (currently a non-functional `@Service()` stub)
- Modify: `frontend/src/app/admin/features/pages/categories/categories.ts` (currently `export class Categories {}`)
- Modify: `frontend/src/app/admin/features/pages/categories/categories.html` (currently `<p>categories works!</p>`)
- Modify: `frontend/src/app/admin/features/pages/categories/categories.scss`
- Modify: `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts` (add a `NAV_ITEMS` entry)
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.routes.server.ts`

**Interfaces:**
- Consumes: `GET|POST api/Admin/Categories`, `GET|PUT|DELETE api/Admin/Categories/{id}`, `PUT api/Admin/Categories/{id}/toggleStatus` (Task 2); `AdminApiEnvelope<T>` and `AdminAuthServices.hasPermission(key)` (Phase 1); `adminPermissionGuard('categories.view')` (Phase 1).
- Produces: `AdminCategoryInterface`, `CategoryTreeRow`, `CategoryServices`, and the component class `Categories` (the existing stub's class name — `app.routes.ts` imports it as `Categories as AdminCategoriesComponent`).

- [ ] **Step 1: Write the interfaces**

```typescript
// frontend/src/app/admin/shared/interface/categoryInterface.ts
export interface AdminCategoryInterface {
  id: number;
  parentId?: number | null;
  title: string;
  slug: string;
  description?: string | null;
  image?: string | null;
  sort?: number | null;
  feature: boolean;
  status: boolean;
  metaDescription?: string | null;
  metaKey?: string | null;
}

// One row of the "Show tree" view, produced client-side by grouping on parentId.
export interface CategoryTreeRow {
  category: AdminCategoryInterface;
  depth: number;
  hasChildren: boolean;
  expanded: boolean;
}
```

- [ ] **Step 2: Write `CategoryServices`**

```typescript
// frontend/src/app/admin/core/services/category-services.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { AdminCategoryInterface } from '../../shared/interface/categoryInterface';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class CategoryServices {
  private http = inject(HttpClient);

  getCategories(): Observable<AdminCategoryInterface[]> {
    return this.http.get<AdminApiEnvelope<AdminCategoryInterface[]>>('/Admin/Categories').pipe(map(response => response.data));
  }

  getCategory(id: number): Observable<AdminCategoryInterface> {
    return this.http.get<AdminApiEnvelope<AdminCategoryInterface>>(`/Admin/Categories/${id}`).pipe(map(response => response.data));
  }

  // Categories are posted as multipart/form-data because the request carries an
  // optional ImageFile. Do not set Content-Type — the browser adds the boundary.
  createCategory(payload: FormData): Observable<AdminCategoryInterface> {
    return this.http.post<AdminApiEnvelope<AdminCategoryInterface>>('/Admin/Categories', payload).pipe(map(response => response.data));
  }

  updateCategory(id: number, payload: FormData): Observable<AdminCategoryInterface> {
    return this.http.put<AdminApiEnvelope<AdminCategoryInterface>>(`/Admin/Categories/${id}`, payload).pipe(map(response => response.data));
  }

  toggleStatus(id: number): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Categories/${id}/toggleStatus`, {}).pipe(map(() => undefined));
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Categories/${id}`).pipe(map(() => undefined));
  }
}
```

- [ ] **Step 3: Write the component**

```typescript
// frontend/src/app/admin/features/pages/categories/categories.ts
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CategoryServices } from '../../../core/services/category-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { AdminCategoryInterface, CategoryTreeRow } from '../../../shared/interface/categoryInterface';
import { Environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-admin-categories',
  imports: [ReactiveFormsModule],
  templateUrl: './categories.html',
  styleUrl: './categories.scss',
})
export class Categories {
  private categoryService = inject(CategoryServices);
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  // Uploaded images are served by the API host, not the Angular dev server.
  private readonly assetOrigin = Environment.apiUrl.replace(/\/api\/?$/, '');

  categories = signal<AdminCategoryInterface[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  treeView = signal(false);
  expandedIds = signal<Set<number>>(new Set());

  selectedFile = signal<File | null>(null);
  existingImage = signal<string | null>(null);

  canManage = () => this.auth.hasPermission('categories.manage');

  form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    slug: ['', Validators.required],
    parentId: [0],
    description: [''],
    sort: [0],
    metaDescription: [''],
    metaKey: [''],
    feature: [false],
    status: [true],
  });

  // Only top-level categories can be picked as a parent, and a category can
  // never be its own parent (the backend rejects that with Category.InvalidParent).
  parentOptions = computed(() =>
    this.categories().filter(c => !c.parentId && c.id !== this.editingId())
  );

  treeRows = computed<CategoryTreeRow[]>(() => {
    const byParent = new Map<number | null, AdminCategoryInterface[]>();
    for (const category of this.categories()) {
      const key = category.parentId ?? null;
      const siblings = byParent.get(key) ?? [];
      siblings.push(category);
      byParent.set(key, siblings);
    }

    const expanded = this.expandedIds();
    const rows: CategoryTreeRow[] = [];

    const walk = (parentId: number | null, depth: number): void => {
      for (const category of byParent.get(parentId) ?? []) {
        const children = byParent.get(category.id) ?? [];
        const isExpanded = expanded.has(category.id);
        rows.push({ category, depth, hasChildren: children.length > 0, expanded: isExpanded });
        if (isExpanded) walk(category.id, depth + 1);
      }
    };

    walk(null, 0);
    return rows;
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.categoryService.getCategories().subscribe({
      next: data => {
        this.categories.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  imageUrl(path?: string | null): string {
    if (!path) return '';
    return /^https?:\/\//i.test(path) ? path : `${this.assetOrigin}${path}`;
  }

  parentTitle(category: AdminCategoryInterface): string {
    if (!category.parentId) return '—';
    return this.categories().find(c => c.id === category.parentId)?.title ?? '—';
  }

  indent(depth: number): string {
    return `${depth * 1.5}rem`;
  }

  toggleTreeView(): void {
    this.treeView.update(v => !v);
  }

  toggleExpanded(id: number): void {
    this.expandedIds.update(ids => {
      const next = new Set(ids);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  startAdd(): void {
    this.editingId.set(null);
    this.selectedFile.set(null);
    this.existingImage.set(null);
    this.form.reset({ title: '', slug: '', parentId: 0, description: '', sort: 0, metaDescription: '', metaKey: '', feature: false, status: true });
    this.showForm.set(true);
  }

  startEdit(category: AdminCategoryInterface): void {
    this.editingId.set(category.id);
    this.selectedFile.set(null);
    this.existingImage.set(category.image ?? null);
    this.form.reset({
      title: category.title,
      slug: category.slug,
      parentId: category.parentId ?? 0,
      description: category.description ?? '',
      sort: category.sort ?? 0,
      metaDescription: category.metaDescription ?? '',
      metaKey: category.metaKey ?? '',
      feature: category.feature,
      status: category.status,
    });
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  private buildFormData(): FormData {
    const raw = this.form.getRawValue();
    const payload = new FormData();

    payload.append('Title', raw.title);
    payload.append('Slug', raw.slug);
    payload.append('Feature', String(raw.feature));
    payload.append('Status', String(raw.status));
    payload.append('Sort', String(raw.sort ?? 0));

    // A <select> always yields a string, so "0" (the "None" option) is truthy —
    // coerce before testing, or a top-level category would post ParentId=0 and
    // blow up on the FK.
    const parentId = Number(raw.parentId);
    if (parentId > 0) payload.append('ParentId', String(parentId));

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
      ? this.categoryService.updateCategory(editingId, payload)
      : this.categoryService.createCategory(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this category. Check the slug is unique and the image is a JPG/PNG/WebP under 2 MB.');
      },
    });
  }

  toggleStatus(category: AdminCategoryInterface): void {
    this.busyId.set(category.id);
    this.categoryService.toggleStatus(category.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(category: AdminCategoryInterface): void {
    this.busyId.set(category.id);
    this.categoryService.deleteCategory(category.id).subscribe({
      next: () => {
        this.categories.update(items => items.filter(c => c.id !== category.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
```

- [ ] **Step 4: Write the template**

```html
<!-- frontend/src/app/admin/features/pages/categories/categories.html -->
<div class="panel-header">
  <div>
    <h1 class="page-title">Categories</h1>
    <p class="page-subtitle">Organise the catalogue into a parent/child hierarchy.</p>
  </div>
  <div class="header-actions">
    @if (!showForm()) {
      <button type="button" class="toggle-btn" (click)="toggleTreeView()">
        {{ treeView() ? 'Show table' : 'Show tree' }}
      </button>
      @if (canManage()) {
        <button type="button" class="add-btn" (click)="startAdd()">+ Add Category</button>
      }
    }
  </div>
</div>

@if (loading()) {
  <div class="state-message">Loading categories…</div>
} @else if (!showForm()) {
  @if (!treeView()) {
    <table class="data-table">
      <thead>
        <tr>
          <th>Image</th>
          <th>Title</th>
          <th>Slug</th>
          <th>Parent</th>
          <th>Sort</th>
          <th>Status</th>
          @if (canManage()) { <th>Actions</th> }
        </tr>
      </thead>
      <tbody>
        @for (category of categories(); track category.id) {
          <tr>
            <td>
              @if (category.image) {
                <img class="thumb" [src]="imageUrl(category.image)" [alt]="category.title">
              } @else {
                <span class="thumb thumb-empty">—</span>
              }
            </td>
            <td>{{ category.title }}</td>
            <td class="muted">{{ category.slug }}</td>
            <td>{{ parentTitle(category) }}</td>
            <td>{{ category.sort ?? '—' }}</td>
            <td>
              <span class="pill" [class.pill-off]="!category.status">{{ category.status ? 'Active' : 'Hidden' }}</span>
            </td>
            @if (canManage()) {
              <td class="actions">
                <button type="button" (click)="startEdit(category)">Edit</button>
                <button type="button" [disabled]="busyId() === category.id" (click)="toggleStatus(category)">Toggle</button>
                <button type="button" class="danger" [disabled]="busyId() === category.id" (click)="remove(category)">Delete</button>
              </td>
            }
          </tr>
        } @empty {
          <tr><td colspan="7" class="state-message">No categories yet.</td></tr>
        }
      </tbody>
    </table>
  } @else {
    <div class="tree">
      @for (row of treeRows(); track row.category.id) {
        <div class="tree-row" [style.padding-left]="indent(row.depth)">
          @if (row.hasChildren) {
            <button type="button" class="expander" (click)="toggleExpanded(row.category.id)">
              <i class="bi" [class.bi-chevron-down]="row.expanded" [class.bi-chevron-right]="!row.expanded"></i>
            </button>
          } @else {
            <span class="expander expander-leaf"></span>
          }

          <span class="tree-title">{{ row.category.title }}</span>
          <span class="pill" [class.pill-off]="!row.category.status">{{ row.category.status ? 'Active' : 'Hidden' }}</span>

          @if (canManage()) {
            <span class="tree-actions">
              <button type="button" (click)="startEdit(row.category)">Edit</button>
              <button type="button" class="danger" [disabled]="busyId() === row.category.id" (click)="remove(row.category)">Delete</button>
            </span>
          }
        </div>
      } @empty {
        <div class="state-message">No categories yet.</div>
      }
    </div>
  }
}

@if (showForm()) {
  @if (error()) {
    <div class="alert-error">{{ error() }}</div>
  }

  <form [formGroup]="form" (ngSubmit)="save()" class="category-form">
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

    <div class="field-group">
      <label>Parent</label>
      <select formControlName="parentId" class="form-control">
        <option [value]="0">— None (top level) —</option>
        @for (parent of parentOptions(); track parent.id) {
          <option [value]="parent.id">{{ parent.title }}</option>
        }
      </select>
    </div>

    <div class="field-group">
      <label>Description</label>
      <textarea formControlName="description" rows="3" class="form-control"></textarea>
    </div>

    <div class="field-group">
      <label>Image</label>
      @if (existingImage()) {
        <img class="preview" [src]="imageUrl(existingImage())" alt="Current image">
      }
      <input type="file" accept="image/*" class="form-control" (change)="onFileSelected($event)">
      <small class="muted">JPG, PNG or WebP, up to 2 MB. Leave empty to keep the current image.</small>
    </div>

    <div class="field-row">
      <div class="field-group">
        <label>Sort</label>
        <input formControlName="sort" type="number" class="form-control">
      </div>
      <div class="field-group">
        <label>Meta key</label>
        <input formControlName="metaKey" type="text" class="form-control">
      </div>
    </div>

    <div class="field-group">
      <label>Meta description</label>
      <input formControlName="metaDescription" type="text" class="form-control">
    </div>

    <div class="checkbox-row">
      <label class="checkbox-field"><input formControlName="feature" type="checkbox"> Featured</label>
      <label class="checkbox-field"><input formControlName="status" type="checkbox"> Active</label>
    </div>

    <div class="form-actions">
      <button type="submit" class="save-btn" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save Category' }}</button>
      <button type="button" class="cancel-btn" (click)="cancel()">Cancel</button>
    </div>
  </form>
}
```

- [ ] **Step 5: Write the styles**

```scss
// frontend/src/app/admin/features/pages/categories/categories.scss
@import '../../../shared/scss/variables';

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1.5rem;
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
  width: 120px;
  height: 120px;
  object-fit: cover;
  border-radius: 10px;
  margin-bottom: 0.5rem;
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

.tree {
  background: #fff;
  border-radius: $admin-radius;
  padding: 0.5rem 0;
}

.tree-row {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.55rem 1rem;
  border-bottom: 1px solid rgba(0, 0, 0, 0.05);
}

.expander {
  width: 1.4rem;
  border: none;
  background: transparent;
  cursor: pointer;
  color: $admin-muted;
}

.expander-leaf {
  display: inline-block;
  cursor: default;
}

.tree-title {
  font-weight: 600;
  color: $admin-text;
}

.tree-actions {
  margin-left: auto;
}

.actions button, .tree-actions button {
  margin-right: 0.5rem;
  border: none;
  background: transparent;
  cursor: pointer;
  font-weight: 600;

  &.danger { color: #b3261e; }
  &:disabled { opacity: 0.4; cursor: not-allowed; }
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

.category-form {
  background: #fff;
  border-radius: $admin-radius;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 640px;
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

In `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts`, add one entry to `NAV_ITEMS` between Dashboard and Roles:

```typescript
const NAV_ITEMS: AdminNavItem[] = [
  { label: 'Dashboard', path: '.', icon: 'bi-grid-1x2-fill', permission: 'dashboard.view' },
  { label: 'Categories', path: 'categories', icon: 'bi-diagram-3-fill', permission: 'categories.view' },
  { label: 'Roles', path: 'roles', icon: 'bi-shield-lock-fill', permission: 'roles.manage' },
  { label: 'Admins', path: 'admins', icon: 'bi-people-fill', permission: 'admins.manage' },
];
```

- [ ] **Step 7: Add the route**

In `frontend/src/app/app.routes.ts`, add the import (the site tree already exports a `CategoriesComponent`, so alias this one):

```typescript
import { Categories as AdminCategoriesComponent } from './admin/features/pages/categories/categories';
```

and add the child route inside the `path: 'admin'` block, after the dashboard entry:

```typescript
        { path: 'categories', component: AdminCategoriesComponent, canActivate: [adminPermissionGuard('categories.view')], title: 'Categories' },
```

- [ ] **Step 8: Add the server render mode**

In `frontend/src/app/app.routes.server.ts`, add alongside the other admin entries (before the `'**'` catch-all):

```typescript
  {
    path: 'admin/categories',
    renderMode: RenderMode.Client
  },
```

- [ ] **Step 9: Type-check**

```powershell
npx tsc --noEmit -p frontend/tsconfig.app.json
```

Expected: 0 errors.

- [ ] **Step 10: Manually verify**

Start the backend (`dotnet run --project backend/Ecommerce`) and the frontend (`npm start` from `frontend/`), log in at `http://localhost:4200/admin/auth/login` as `admin.tester@example.com` / `AdminTester@123`, and confirm:

1. A **Categories** item appears in the sidebar; clicking it opens `/admin/categories` showing the seeded categories in a flat table with a **Parent** column reading `—` for every top-level row.
2. Click **+ Add Category**, fill in Title `Test Parent` / Slug `test-parent`, pick a JPG or PNG under 2 MB, save. The row appears with a visible thumbnail (proving the API-origin `imageUrl()` prefix works).
3. Click **+ Add Category** again, Title `Test Child` / Slug `test-child`, Parent `Test Parent`, save. The new row's **Parent** column reads `Test Parent`.
4. Click **Show tree** — the view switches to an indented list with a chevron on `Test Parent`. Click the chevron; `Test Child` appears indented beneath it. Click again; it collapses. Click **Show table** to switch back.
5. Click **Edit** on `Test Child`, change only the Title, save. The thumbnail is unchanged (the `Image` path round-tripped).
6. Click **Toggle** on `Test Child` — the Status pill flips to `Hidden`; toggle it back.
7. Delete `Test Child`, then `Test Parent`.
8. Create a role with **only** `categories.view` (via `/admin/roles`), assign it to a second admin, log in as them: the Categories page loads read-only — no `+ Add Category`, no Edit/Toggle/Delete buttons — and `/admin/roles` is not reachable from the sidebar.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/app/admin/shared/interface/categoryInterface.ts frontend/src/app/admin/core/services/category-services.ts frontend/src/app/admin/features/pages/categories/categories.ts frontend/src/app/admin/features/pages/categories/categories.html frontend/src/app/admin/features/pages/categories/categories.scss frontend/src/app/admin/features/layouts/main-layout/main-layout.ts frontend/src/app/app.routes.ts frontend/src/app/app.routes.server.ts
git commit -m "Add Categories admin page with flat table and tree toggle"
```

---

## Task 4: `IClientService` / `ClientService`

**Files:**
- Create: `backend/Ecommerce/Contracts/Clients/ClientResponse.cs`
- Create: `backend/Ecommerce/Contracts/Clients/ClientDetailResponse.cs`
- Create: `backend/Ecommerce/Contracts/Clients/ClientsPageResponse.cs`
- Create: `backend/Ecommerce/Contracts/Clients/UpdateClientRequest.cs`
- Create: `backend/Ecommerce/Errors/ClientErrors.cs`
- Create: `backend/Ecommerce/Services/IClientService.cs`
- Create: `backend/Ecommerce/Services/ClientService.cs`
- Test: `backend/Ecommerce.Tests/Services/ClientServiceTests.cs`

**Interfaces:**
- Consumes: `ApplicationUser` (implements `IAuditable` per Plan 2A), `Order { UserId, Total }`, `UserManager<ApplicationUser>` (already registered by `services.AddIdentity<ApplicationUser, IdentityRole>()` in `DependacyInjection.cs`).
- Produces:
  ```csharp
  public interface IClientService
  {
      Task<Result<ClientsPageResponse>> GetAllAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
      Task<Result<ClientDetailResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
      Task<Result<ClientResponse>> UpdateAsync(string id, UpdateClientRequest request, CancellationToken cancellationToken = default);
      Task<Result> ToggleStatusAsync(string id, CancellationToken cancellationToken = default);
      Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default);
  }
  ```
- Produces: `ClientResponse(string Id, string FirstName, string LastName, string Email, string? PhoneNumber, bool IsActive, bool EmailConfirmed)`; `ClientDetailResponse(string Id, string FirstName, string LastName, string Email, string? PhoneNumber, bool IsActive, bool EmailConfirmed, int OrderCount, double LifetimeTotal)`; `ClientsPageResponse(IReadOnlyList<ClientResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages)`; `UpdateClientRequest(string FirstName, string LastName, string Email, string? PhoneNumber)`.
- Produces: `ClientErrors.ClientNotFound` (`"Client.NotFound"`), `ClientErrors.EmailAlreadyExists` (`"Client.EmailAlreadyExists"`), `ClientErrors.UpdateFailed` (`"Client.UpdateFailed"`).
- Task 5's `AdminClientsController` and Task 6's `ClientServices` both depend on these exact names.

- [ ] **Step 1: Write the contracts**

```csharp
// backend/Ecommerce/Contracts/Clients/ClientResponse.cs
namespace Ecommerce.Contracts.Clients;

// IsActive is projected from Identity's lockout state — there is no IsActive column.
public record ClientResponse(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool EmailConfirmed);
```

```csharp
// backend/Ecommerce/Contracts/Clients/ClientDetailResponse.cs
namespace Ecommerce.Contracts.Clients;

public record ClientDetailResponse(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    bool EmailConfirmed,
    int OrderCount,
    double LifetimeTotal);
```

```csharp
// backend/Ecommerce/Contracts/Clients/ClientsPageResponse.cs
namespace Ecommerce.Contracts.Clients;

public record ClientsPageResponse(
    IReadOnlyList<ClientResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
```

```csharp
// backend/Ecommerce/Contracts/Clients/UpdateClientRequest.cs
namespace Ecommerce.Contracts.Clients;

public record UpdateClientRequest(string FirstName, string LastName, string Email, string? PhoneNumber);

public class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
    }
}
```

- [ ] **Step 2: Write the errors**

```csharp
// backend/Ecommerce/Errors/ClientErrors.cs
namespace Ecommerce.Errors;

public static class ClientErrors
{
    public static readonly Error ClientNotFound = new("Client.NotFound", "No client was found with the given ID");
    public static readonly Error EmailAlreadyExists = new("Client.EmailAlreadyExists", "Another account already uses this email address");
    public static readonly Error UpdateFailed = new("Client.UpdateFailed", "The client account could not be updated");
}
```

- [ ] **Step 3: Write the failing tests**

`ClientService` needs a real `UserManager<ApplicationUser>` — mocking it is not worth the ceremony, and the point of the email tests is that Identity's normalization actually runs. Build one over the same in-memory `ApplicationDbContext`:

```csharp
// backend/Ecommerce.Tests/Services/ClientServiceTests.cs
using Ecommerce.Contracts.Clients;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ecommerce.Tests.Services;

public class ClientServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext context) => new(
        new UserStore<ApplicationUser>(context),
        Options.Create(new IdentityOptions()),
        new PasswordHasher<ApplicationUser>(),
        [new UserValidator<ApplicationUser>()],
        [new PasswordValidator<ApplicationUser>()],
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        null!,
        NullLogger<UserManager<ApplicationUser>>.Instance);

    private static async Task<ApplicationUser> SeedClientAsync(
        UserManager<ApplicationUser> userManager,
        string email = "buyer@example.com",
        string firstName = "Bea",
        string lastName = "Buyer")
    {
        var user = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            UserName = email,
            Email = email,
            PhoneNumber = "0100000000",
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(user, "Client@123");
        Assert.True(created.Succeeded);
        return user;
    }

    [Fact]
    public async Task GetAllAsync_filters_by_search_term()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        await SeedClientAsync(userManager, "bea@example.com", "Bea", "Buyer");
        await SeedClientAsync(userManager, "carl@example.com", "Carl", "Customer");
        var service = new ClientService(context, userManager);

        var result = await service.GetAllAsync("carl", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal("carl@example.com", result.Value.Items[0].Email);
    }

    [Fact]
    public async Task GetAllAsync_pages_the_result_set()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        await SeedClientAsync(userManager, "a@example.com", "Anna", "One");
        await SeedClientAsync(userManager, "b@example.com", "Bob", "Two");
        await SeedClientAsync(userManager, "c@example.com", "Cara", "Three");
        var service = new ClientService(context, userManager);

        var result = await service.GetAllAsync(null, 2, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
        Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task GetByIdAsync_returns_order_count_and_lifetime_total()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager);
        context.Orders.AddRange(
            new Order { OrderNumber = "A-1", UserId = user.Id, SubTotal = 100, ShippingCost = 5, Total = 105 },
            new Order { OrderNumber = "A-2", UserId = user.Id, SubTotal = 40, ShippingCost = 0, Total = 40 });
        await context.SaveChangesAsync();
        var service = new ClientService(context, userManager);

        var result = await service.GetByIdAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.OrderCount);
        Assert.Equal(145d, result.Value.LifetimeTotal);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_fails_for_an_unknown_client()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var service = new ClientService(context, userManager);

        var result = await service.GetByIdAsync(Guid.NewGuid().ToString());

        Assert.False(result.IsSuccess);
        Assert.Equal("Client.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_changes_the_email_and_keeps_the_normalized_values_in_sync()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager, "old@example.com");
        var service = new ClientService(context, userManager);

        var result = await service.UpdateAsync(user.Id, new UpdateClientRequest("Bea", "Updated", "new@example.com", "0111111111"));

        Assert.True(result.IsSuccess);
        Assert.Equal("new@example.com", result.Value.Email);

        var reloaded = await context.Users.FirstAsync(x => x.Id == user.Id);
        Assert.Equal("new@example.com", reloaded.Email);
        Assert.Equal("NEW@EXAMPLE.COM", reloaded.NormalizedEmail);
        Assert.Equal("new@example.com", reloaded.UserName);
        Assert.Equal("NEW@EXAMPLE.COM", reloaded.NormalizedUserName);
        Assert.Equal("Updated", reloaded.LastName);
    }

    [Fact]
    public async Task UpdateAsync_fails_when_the_email_belongs_to_another_client()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager, "first@example.com");
        await SeedClientAsync(userManager, "taken@example.com", "Taken", "Account");
        var service = new ClientService(context, userManager);

        var result = await service.UpdateAsync(user.Id, new UpdateClientRequest("Bea", "Buyer", "taken@example.com", null));

        Assert.False(result.IsSuccess);
        Assert.Equal("Client.EmailAlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task ToggleStatusAsync_disables_and_re_enables_the_account()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager);
        var service = new ClientService(context, userManager);

        Assert.True((await service.ToggleStatusAsync(user.Id)).IsSuccess);

        var disabled = await context.Users.AsNoTracking().FirstAsync(x => x.Id == user.Id);
        Assert.True(disabled.LockoutEnabled);
        Assert.Equal(DateTimeOffset.MaxValue, disabled.LockoutEnd);
        Assert.False((await service.GetByIdAsync(user.Id)).Value.IsActive);

        Assert.True((await service.ToggleStatusAsync(user.Id)).IsSuccess);

        var enabled = await context.Users.AsNoTracking().FirstAsync(x => x.Id == user.Id);
        Assert.Null(enabled.LockoutEnd);
        Assert.True((await service.GetByIdAsync(user.Id)).Value.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_the_client_so_ordinary_queries_skip_them()
    {
        await using var context = CreateContext();
        var userManager = CreateUserManager(context);
        var user = await SeedClientAsync(userManager);
        var service = new ClientService(context, userManager);

        var result = await service.DeleteAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Users.AnyAsync(x => x.Id == user.Id));
        Assert.True(await context.Users.IgnoreQueryFilters().AnyAsync(x => x.Id == user.Id));
    }
}
```

- [ ] **Step 4: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter ClientServiceTests
```

Expected: FAIL — `IClientService`/`ClientService` and the `Ecommerce.Contracts.Clients` namespace don't exist yet.

- [ ] **Step 5: Write `IClientService`**

```csharp
// backend/Ecommerce/Services/IClientService.cs
using Ecommerce.Contracts.Clients;

namespace Ecommerce.Services;

public interface IClientService
{
    Task<Result<ClientsPageResponse>> GetAllAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Result<ClientDetailResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Result<ClientResponse>> UpdateAsync(string id, UpdateClientRequest request, CancellationToken cancellationToken = default);
    Task<Result> ToggleStatusAsync(string id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Write `ClientService`**

```csharp
// backend/Ecommerce/Services/ClientService.cs
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
```

- [ ] **Step 7: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter ClientServiceTests
```

Expected: 8 passed.

- [ ] **Step 8: Commit**

```bash
git add backend/Ecommerce/Contracts/Clients backend/Ecommerce/Errors/ClientErrors.cs backend/Ecommerce/Services/IClientService.cs backend/Ecommerce/Services/ClientService.cs backend/Ecommerce.Tests/Services/ClientServiceTests.cs
git commit -m "Add IClientService/ClientService for admin management of customer accounts"
```

---

## Task 5: `AdminClientsController`

**Files:**
- Create: `backend/Ecommerce/Controllers/AdminClientsController.cs`
- Modify: `backend/Ecommerce/DependacyInjection.cs` (register `IClientService`)

**Interfaces:**
- Consumes: `IClientService` (`GetAllAsync`, `GetByIdAsync`, `UpdateAsync`, `ToggleStatusAsync`, `DeleteAsync`) and the `Ecommerce.Contracts.Clients` records (Task 4); `PermissionKeys.ClientsView` / `PermissionKeys.ClientsManage`; `AdminAuthDefaults.Scheme`.
- Produces: `GET api/Admin/Clients?search=&page=&pageSize=` → `ApiResponse<ClientsPageResponse>`; `GET api/Admin/Clients/{id}` → `ApiResponse<ClientDetailResponse>`; `PUT api/Admin/Clients/{id}` → `ApiResponse<ClientResponse>`; `PUT api/Admin/Clients/{id}/toggleStatus`; `DELETE api/Admin/Clients/{id}` — Task 6's `ClientServices` calls all five. Note the route id is a **GUID string**, not a long, so no `:long` route constraint.

- [ ] **Step 1: Write the controller**

```csharp
// backend/Ecommerce/Controllers/AdminClientsController.cs
using Ecommerce.Authorization;
using Ecommerce.Contracts.Clients;
using Ecommerce.Contracts.Common;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Clients")]
[ApiController]
public class AdminClientsController(IClientService clientService) : ControllerBase
{
    private readonly IClientService _clientService = clientService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.ClientsView)]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _clientService.GetAllAsync(search, page, pageSize, cancellationToken);
        return Ok(new ApiResponse<ClientsPageResponse>(StatusCodes.Status200OK, "Clients loaded.", result.Value));
    }

    [HttpGet("{id}")]
    [HasPermission(PermissionKeys.ClientsView)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _clientService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Client not found."));

        return Ok(new ApiResponse<ClientDetailResponse>(StatusCodes.Status200OK, "Client loaded.", result.Value));
    }

    [HttpPut("{id}")]
    [HasPermission(PermissionKeys.ClientsManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] string id, [FromBody] UpdateClientRequest request, CancellationToken cancellationToken)
    {
        var result = await _clientService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update client."));

        return Ok(new ApiResponse<ClientResponse>(StatusCodes.Status200OK, "Client updated.", result.Value));
    }

    [HttpPut("{id}/toggleStatus")]
    [HasPermission(PermissionKeys.ClientsManage)]
    public async Task<IActionResult> ToggleStatusAsync([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _clientService.ToggleStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update client status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Client status updated."));
    }

    [HttpDelete("{id}")]
    [HasPermission(PermissionKeys.ClientsManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] string id, CancellationToken cancellationToken)
    {
        var result = await _clientService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete client."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Client deleted."));
    }
}
```

- [ ] **Step 2: Register the service**

In `backend/Ecommerce/DependacyInjection.cs`, inside `AddDependancies`, add after `services.AddScoped<IAdminService, AdminService>();`:

```csharp
            services.AddScoped<IClientService, ClientService>();
```

- [ ] **Step 3: Build and run the suite**

```powershell
dotnet build Ecommerce.slnx
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: 0 build errors, all tests passing.

- [ ] **Step 4: Manually verify the endpoints**

```powershell
dotnet run --project backend/Ecommerce
```

Log in as `admin.tester@example.com` / `AdminTester@123` for a token, then (the dev `DataSeeder` already seeds a customer with three orders):

```bash
curl.exe -k "https://localhost:7297/api/Admin/Clients?page=1&pageSize=5" -H "Authorization: Bearer <token>"
curl.exe -k "https://localhost:7297/api/Admin/Clients?search=test" -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Admin/Clients/<client-id> -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Admin/Clients
```

Expected: the first three return `200` — a paged list carrying `totalCount`/`totalPages`, a filtered list, and a detail body whose `orderCount`/`lifetimeTotal` match the seeded orders. The unauthenticated call returns `401`.

Then confirm the lockout toggle really gates login:

```bash
curl.exe -k -X PUT https://localhost:7297/api/Admin/Clients/<client-id>/toggleStatus -H "Authorization: Bearer <token>"
```

Expected: `200`, and a subsequent `POST https://localhost:7297/api/Auth/login` as that customer is rejected. Toggle it back and confirm the customer can log in again.

- [ ] **Step 5: Commit**

```bash
git add backend/Ecommerce/Controllers/AdminClientsController.cs backend/Ecommerce/DependacyInjection.cs
git commit -m "Add AdminClientsController for viewing, editing, toggling and deleting clients"
```

---

## Task 6: Clients admin page

**Files:**
- Create: `frontend/src/app/admin/shared/interface/client-interfaces.ts`
- Create: `frontend/src/app/admin/core/services/client-services.ts`
- Create: `frontend/src/app/admin/features/pages/clients/clients.ts`
- Create: `frontend/src/app/admin/features/pages/clients/clients.html`
- Create: `frontend/src/app/admin/features/pages/clients/clients.scss`
- Modify: `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts` (add a `NAV_ITEMS` entry)
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.routes.server.ts`

**Interfaces:**
- Consumes: `GET api/Admin/Clients?search=&page=&pageSize=`, `GET|PUT|DELETE api/Admin/Clients/{id}`, `PUT api/Admin/Clients/{id}/toggleStatus` (Task 5); `AdminApiEnvelope<T>` and `AdminAuthServices.hasPermission(key)` (Phase 1); `adminPermissionGuard('clients.view')` (Phase 1).
- Produces: `ClientInterface`, `ClientDetailInterface`, `ClientsPageInterface`, `UpdateClientRequest`, `ClientServices`, and the component class `ClientsComponent` — `app.routes.ts` imports it under that exact name.

- [ ] **Step 1: Write the interfaces**

```typescript
// frontend/src/app/admin/shared/interface/client-interfaces.ts
export interface ClientInterface {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string | null;
  isActive: boolean;
  emailConfirmed: boolean;
}

export interface ClientDetailInterface extends ClientInterface {
  orderCount: number;
  lifetimeTotal: number;
}

export interface ClientsPageInterface {
  items: ClientInterface[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UpdateClientRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
}
```

- [ ] **Step 2: Write `ClientServices`**

```typescript
// frontend/src/app/admin/core/services/client-services.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { ClientDetailInterface, ClientInterface, ClientsPageInterface, UpdateClientRequest } from '../../shared/interface/client-interfaces';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class ClientServices {
  private http = inject(HttpClient);

  getClients(search: string, page: number, pageSize: number): Observable<ClientsPageInterface> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);

    return this.http.get<AdminApiEnvelope<ClientsPageInterface>>('/Admin/Clients', { params }).pipe(map(response => response.data));
  }

  getClient(id: string): Observable<ClientDetailInterface> {
    return this.http.get<AdminApiEnvelope<ClientDetailInterface>>(`/Admin/Clients/${id}`).pipe(map(response => response.data));
  }

  updateClient(id: string, request: UpdateClientRequest): Observable<ClientInterface> {
    return this.http.put<AdminApiEnvelope<ClientInterface>>(`/Admin/Clients/${id}`, request).pipe(map(response => response.data));
  }

  toggleStatus(id: string): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Clients/${id}/toggleStatus`, {}).pipe(map(() => undefined));
  }

  deleteClient(id: string): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Clients/${id}`).pipe(map(() => undefined));
  }
}
```

- [ ] **Step 3: Write the component**

```typescript
// frontend/src/app/admin/features/pages/clients/clients.ts
import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ClientServices } from '../../../core/services/client-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { ClientDetailInterface, ClientInterface } from '../../../shared/interface/client-interfaces';

@Component({
  selector: 'app-admin-clients',
  imports: [ReactiveFormsModule, CurrencyPipe],
  templateUrl: './clients.html',
  styleUrl: './clients.scss',
})
export class ClientsComponent {
  private clientService = inject(ClientServices);
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  private readonly pageSize = 20;

  clients = signal<ClientInterface[]>([]);
  page = signal(1);
  totalPages = signal(0);
  totalCount = signal(0);
  searchTerm = signal('');

  detail = signal<ClientDetailInterface | null>(null);

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<string | null>(null);
  busyId = signal<string | null>(null);

  canManage = () => this.auth.hasPermission('clients.manage');

  form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: [''],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.clientService.getClients(this.searchTerm(), this.page(), this.pageSize).subscribe({
      next: data => {
        this.clients.set(data.items);
        this.totalPages.set(data.totalPages);
        this.totalCount.set(data.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onSearchInput(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  search(): void {
    this.page.set(1);
    this.detail.set(null);
    this.load();
  }

  goToPage(page: number): void {
    if (page < 1 || (this.totalPages() > 0 && page > this.totalPages())) return;
    this.page.set(page);
    this.detail.set(null);
    this.load();
  }

  view(client: ClientInterface): void {
    this.detail.set(null);
    this.busyId.set(client.id);
    this.clientService.getClient(client.id).subscribe({
      next: data => {
        this.detail.set(data);
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  closeDetail(): void {
    this.detail.set(null);
  }

  startEdit(client: ClientInterface): void {
    this.editingId.set(client.id);
    this.form.reset({
      firstName: client.firstName,
      lastName: client.lastName,
      email: client.email,
      phoneNumber: client.phoneNumber ?? '',
    });
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  save(): void {
    const editingId = this.editingId();
    if (!editingId) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();
    this.clientService.updateClient(editingId, {
      firstName: raw.firstName,
      lastName: raw.lastName,
      email: raw.email,
      phoneNumber: raw.phoneNumber || undefined,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this client. Check the email is not already used by another account.');
      },
    });
  }

  toggleStatus(client: ClientInterface): void {
    this.busyId.set(client.id);
    this.clientService.toggleStatus(client.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(client: ClientInterface): void {
    this.busyId.set(client.id);
    this.clientService.deleteClient(client.id).subscribe({
      next: () => {
        this.clients.update(items => items.filter(c => c.id !== client.id));
        this.totalCount.update(count => count - 1);
        this.detail.set(null);
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
```

- [ ] **Step 4: Write the template**

```html
<!-- frontend/src/app/admin/features/pages/clients/clients.html -->
<div class="panel-header">
  <div>
    <h1 class="page-title">Clients</h1>
    <p class="page-subtitle">Customer accounts — {{ totalCount() }} total.</p>
  </div>
  @if (!showForm()) {
    <div class="search-box">
      <input
        type="search"
        class="form-control"
        placeholder="Search name or email…"
        [value]="searchTerm()"
        (input)="onSearchInput($event)"
        (keyup.enter)="search()">
      <button type="button" class="add-btn" (click)="search()">Search</button>
    </div>
  }
</div>

@if (loading()) {
  <div class="state-message">Loading clients…</div>
} @else if (!showForm()) {
  <table class="data-table">
    <thead>
      <tr>
        <th>Client</th>
        <th>Email</th>
        <th>Phone</th>
        <th>Status</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      @for (client of clients(); track client.id) {
        <tr>
          <td>
            <span class="avatar">{{ client.firstName.charAt(0) }}</span>
            {{ client.firstName }} {{ client.lastName }}
          </td>
          <td>
            {{ client.email }}
            @if (!client.emailConfirmed) {
              <div class="muted">Email not confirmed</div>
            }
          </td>
          <td>{{ client.phoneNumber || '—' }}</td>
          <td>
            <span class="pill" [class.pill-off]="!client.isActive">{{ client.isActive ? 'Active' : 'Disabled' }}</span>
          </td>
          <td class="actions">
            <button type="button" [disabled]="busyId() === client.id" (click)="view(client)">View</button>
            @if (canManage()) {
              <button type="button" (click)="startEdit(client)">Edit</button>
              <button type="button" [disabled]="busyId() === client.id" (click)="toggleStatus(client)">
                {{ client.isActive ? 'Disable' : 'Enable' }}
              </button>
              <button type="button" class="danger" [disabled]="busyId() === client.id" (click)="remove(client)">Delete</button>
            }
          </td>
        </tr>
      } @empty {
        <tr><td colspan="5" class="state-message">No clients match this search.</td></tr>
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

  @if (detail(); as client) {
    <div class="detail-card">
      <div class="detail-header">
        <h2>{{ client.firstName }} {{ client.lastName }}</h2>
        <button type="button" class="cancel-btn" (click)="closeDetail()">Close</button>
      </div>
      <dl class="detail-grid">
        <div><dt>Email</dt><dd>{{ client.email }}</dd></div>
        <div><dt>Phone</dt><dd>{{ client.phoneNumber || '—' }}</dd></div>
        <div><dt>Status</dt><dd>{{ client.isActive ? 'Active' : 'Disabled' }}</dd></div>
        <div><dt>Orders</dt><dd>{{ client.orderCount }}</dd></div>
        <div><dt>Lifetime total</dt><dd>{{ client.lifetimeTotal | currency }}</dd></div>
      </dl>
    </div>
  }
}

@if (showForm()) {
  @if (error()) {
    <div class="alert-error">{{ error() }}</div>
  }

  <form [formGroup]="form" (ngSubmit)="save()" class="client-form">
    <div class="field-row">
      <div class="field-group">
        <label>First Name</label>
        <input formControlName="firstName" type="text" class="form-control">
      </div>
      <div class="field-group">
        <label>Last Name</label>
        <input formControlName="lastName" type="text" class="form-control">
      </div>
    </div>
    <div class="field-group">
      <label>Email</label>
      <input formControlName="email" type="email" class="form-control">
      <small class="muted">Changing this changes the account's sign-in address.</small>
    </div>
    <div class="field-group">
      <label>Phone</label>
      <input formControlName="phoneNumber" type="tel" class="form-control">
    </div>

    <div class="form-actions">
      <button type="submit" class="save-btn" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save Client' }}</button>
      <button type="button" class="cancel-btn" (click)="cancel()">Cancel</button>
    </div>
  </form>
}
```

- [ ] **Step 5: Write the styles**

```scss
// frontend/src/app/admin/features/pages/clients/clients.scss
@import '../../../shared/scss/variables';

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1.5rem;
  gap: 1rem;
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

  .form-control { min-width: 260px; }
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

.cancel-btn {
  background: transparent;
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.65rem 1.1rem;
  font-weight: 600;
  cursor: pointer;
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

.avatar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 1.75rem;
  height: 1.75rem;
  border-radius: 50%;
  background: rgba($admin-green, 0.12);
  color: $admin-green;
  font-weight: 700;
  margin-right: 0.5rem;
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

.detail-card {
  background: #fff;
  border-radius: $admin-radius;
  padding: 1.25rem 1.5rem;
  margin-top: 1.5rem;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: center;

  h2 {
    font-size: 1.1rem;
    font-weight: 800;
    color: $admin-text;
    margin: 0;
  }
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 1rem;
  margin: 1rem 0 0;

  dt {
    color: $admin-muted;
    font-size: 0.75rem;
    text-transform: uppercase;
  }

  dd {
    margin: 0.2rem 0 0;
    font-weight: 600;
    color: $admin-text;
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

.client-form {
  background: #fff;
  border-radius: $admin-radius;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 560px;
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

.form-actions {
  display: flex;
  gap: 0.75rem;
}
```

- [ ] **Step 6: Add the sidebar nav entry**

In `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts`, add one entry to `NAV_ITEMS` after Categories:

```typescript
  { label: 'Clients', path: 'clients', icon: 'bi-person-lines-fill', permission: 'clients.view' },
```

- [ ] **Step 7: Add the route**

In `frontend/src/app/app.routes.ts`, add the import:

```typescript
import { ClientsComponent } from './admin/features/pages/clients/clients';
```

and add the child route inside the `path: 'admin'` block, after the categories entry:

```typescript
        { path: 'clients', component: ClientsComponent, canActivate: [adminPermissionGuard('clients.view')], title: 'Clients' },
```

- [ ] **Step 8: Add the server render mode**

In `frontend/src/app/app.routes.server.ts`, add alongside the other admin entries (before the `'**'` catch-all):

```typescript
  {
    path: 'admin/clients',
    renderMode: RenderMode.Client
  },
```

- [ ] **Step 9: Type-check**

```powershell
npx tsc --noEmit -p frontend/tsconfig.app.json
```

Expected: 0 errors.

- [ ] **Step 10: Manually verify**

With the backend and frontend running, logged in at `http://localhost:4200/admin/auth/login` as `admin.tester@example.com` / `AdminTester@123`:

1. A **Clients** item appears in the sidebar; clicking it opens `/admin/clients` listing the seeded customer(s), with the total count in the subtitle.
2. Type part of the seeded customer's first name into the search box and press Enter — the list narrows to matching rows. Clear it and search again to get the full list back.
3. Click **View** on the seeded customer — a detail card appears showing **Orders** and **Lifetime total** matching the three seeded orders.
4. Click **Edit**, change the last name and the phone number, save. The row updates.
5. Click **Edit** again, change the email to `renamed.customer@example.com`, save. Then open a second browser (or a private window), go to `http://localhost:4200/auth/login`, and confirm the customer can log in **with the new email** — this is the proof that `SetEmailAsync`/`SetUserNameAsync` kept the normalized columns in sync.
6. Back in the admin, click **Disable** on that client — the Status pill flips to `Disabled`. Try logging in as the customer again: it is rejected. Click **Enable** and confirm login works again.
7. Click **Delete** on the client — the row disappears and the total count drops. Confirm the customer can no longer log in.
8. Create a role with **only** `clients.view`, assign it to a second admin, log in as them: the Clients page loads with only the **View** button on each row — no Edit/Disable/Delete and no ability to reach `/admin/roles`.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/app/admin/shared/interface/client-interfaces.ts frontend/src/app/admin/core/services/client-services.ts frontend/src/app/admin/features/pages/clients frontend/src/app/admin/features/layouts/main-layout/main-layout.ts frontend/src/app/app.routes.ts frontend/src/app/app.routes.server.ts
git commit -m "Add Clients admin page"
```

---

## Task 7: `Slider` entity, EF configuration, `sliders.view` permission, and the `AddSliders` migration

**Files:**
- Create: `backend/Ecommerce/Entities/Slider.cs`
- Create: `backend/Ecommerce/Presistence/EntitiesConfigurations/SliderConfiguration.cs`
- Modify: `backend/Ecommerce/Presistence/ApplicationDbContext.cs` (add the `DbSet`)
- Modify: `backend/Ecommerce/Authorization/PermissionKeys.cs` (add `SlidersView`)
- Test: `backend/Ecommerce.Tests/Entities/SliderModelTests.cs`

**Interfaces:**
- Consumes: `AuditableEntity` (Plan 2A) — supplies `CreatedById`, `CreatedOn`, `UpdatedById`, `UpdatedOn`, `IsDeleted`, `DeletedOn`, `DeletedById` and the `CreatedBy`/`UpdatedBy`/`DeletedBy` nav props, all stamped automatically by the DbContext hook.
- Produces: `Ecommerce.Entities.Slider { long Id, string Title, string Image, string? Link, int? Sort, bool Status, DateTime? StartsOn, DateTime? EndsOn }` plus the audit base — Task 8's `SliderService` and Task 9's controllers use exactly these property names.
- Produces: `ApplicationDbContext.Sliders`.
- Produces: `PermissionKeys.SlidersView = "sliders.view"` and its `Catalog` entry — Task 9's `[HasPermission(PermissionKeys.SlidersView)]` and Task 10's `adminPermissionGuard('sliders.view')` both depend on it.

**Migration note.** The Phase 2 design doc says `Slider` ships in the same migration as the audit columns. Because Phase 2 is split across two plans, that is **not** what happens here: Plan 2A owns `AddAuditAndSoftDelete` (pre-existing entities only), and `Slider` gets its own migration, **`AddSliders`**, in this task. Since 2A has already run, `AddSliders` creates the `Sliders` table with its audit columns and `Admin` FKs included.

- [ ] **Step 1: Write the entity**

```csharp
// backend/Ecommerce/Entities/Slider.cs
namespace Ecommerce.Entities;

// Homepage carousel slide. Image holds the path returned by IFileStorage
// (e.g. /uploads/sliders/<guid>.jpg). StartsOn/EndsOn are an optional
// scheduling window evaluated server-side by the public endpoint.
public sealed class Slider : AuditableEntity
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string? Link { get; set; }
    public int? Sort { get; set; }
    public bool Status { get; set; } = true;
    public DateTime? StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
}
```

- [ ] **Step 2: Write the EF configuration**

```csharp
// backend/Ecommerce/Presistence/EntitiesConfigurations/SliderConfiguration.cs
namespace Ecommerce.Presistence.EntitiesConfigurations;

public class SliderConfiguration : IEntityTypeConfiguration<Slider>
{
    public void Configure(EntityTypeBuilder<Slider> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Title).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Image).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Link).HasMaxLength(500);

        builder.HasIndex(x => x.Sort);

        // The CreatedBy/UpdatedBy/DeletedBy FKs to Admin come from AuditableEntity
        // and are discovered by convention; OnModelCreating already rewrites every
        // cascade FK to Restrict, and the !IsDeleted query filter is applied by the
        // reflection loop over IAuditable — nothing to configure here.
    }
}
```

- [ ] **Step 3: Add the `DbSet`**

In `backend/Ecommerce/Presistence/ApplicationDbContext.cs`, add alongside the existing `DbSet<Permission> Permissions`:

```csharp
        public DbSet<Slider> Sliders { get; set; }
```

- [ ] **Step 4: Add the `sliders.view` permission key**

In `backend/Ecommerce/Authorization/PermissionKeys.cs`, add the constant next to `SlidersManage`:

```csharp
    public const string SlidersView = "sliders.view";
    public const string SlidersManage = "sliders.manage";
```

and add the catalog entry immediately before the `SlidersManage` one:

```csharp
        (SlidersView, "Sliders", "View homepage sliders"),
        (SlidersManage, "Sliders", "Manage homepage sliders"),
```

**No migration is needed for this.** `AdminDataSeeder.SeedAsync` inserts any `PermissionKeys.Catalog` entry that is not already in the `Permissions` table and then grants every permission the Super Admin role is missing — both idempotent — so `sliders.view` seeds itself and lands on Super Admin on the next dev run.

- [ ] **Step 5: Write a model test**

```csharp
// backend/Ecommerce.Tests/Entities/SliderModelTests.cs
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Entities;

public class SliderModelTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    [Fact]
    public async Task Slider_round_trips_with_its_scheduling_window()
    {
        await using var context = CreateContext();

        context.Sliders.Add(new Slider
        {
            Title = "Summer sale",
            Image = "/uploads/sliders/summer.jpg",
            Link = "/products",
            Sort = 1,
            Status = true,
            StartsOn = new DateTime(2026, 6, 1),
            EndsOn = new DateTime(2026, 8, 31),
        });
        await context.SaveChangesAsync();

        var loaded = await context.Sliders.FirstAsync(x => x.Title == "Summer sale");

        Assert.Equal("/uploads/sliders/summer.jpg", loaded.Image);
        Assert.Equal(1, loaded.Sort);
        Assert.Equal(new DateTime(2026, 8, 31), loaded.EndsOn);
        Assert.False(loaded.IsDeleted);
    }

    [Fact]
    public async Task Removing_a_slider_soft_deletes_it()
    {
        await using var context = CreateContext();
        context.Sliders.Add(new Slider { Title = "Old", Image = "/uploads/sliders/old.jpg" });
        await context.SaveChangesAsync();

        var slider = await context.Sliders.FirstAsync();
        context.Remove(slider);
        await context.SaveChangesAsync();

        Assert.False(await context.Sliders.AnyAsync(x => x.Id == slider.Id));
        Assert.True(await context.Sliders.IgnoreQueryFilters().AnyAsync(x => x.Id == slider.Id));
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter SliderModelTests
```

Expected: 2 passed. (These exercise Plan 2A's hook and query filter through a brand-new entity — if the second test fails, the entity is not being picked up as `IAuditable`; check that `Slider` inherits `AuditableEntity`.)

- [ ] **Step 7: Add and apply the migration**

From `backend/`:

```powershell
dotnet ef migrations add AddSliders --project Ecommerce
dotnet ef database update --project Ecommerce
```

Open the generated migration and verify it creates a single `Sliders` table with `Id`, `Title`, `Image`, `Link`, `Sort`, `Status`, `StartsOn`, `EndsOn`, the six audit columns (`CreatedById`, `CreatedOn`, `UpdatedById`, `UpdatedOn`, `IsDeleted`, `DeletedOn`, `DeletedById`) and three `Restrict` FKs to `Admins`. It must **not** touch any other table — if it does, Plan 2A's `AddAuditAndSoftDelete` was not applied first; stop and resolve that before continuing.

- [ ] **Step 8: Run the app once to seed the new permission**

```powershell
dotnet run --project backend/Ecommerce
```

Watch for an `INSERT` into `Permissions` for `sliders.view` and a matching `AdminRolePermissions` row on first run. Stop and re-run — no new inserts (idempotent).

- [ ] **Step 9: Commit**

```bash
git add backend/Ecommerce/Entities/Slider.cs backend/Ecommerce/Presistence/EntitiesConfigurations/SliderConfiguration.cs backend/Ecommerce/Presistence/ApplicationDbContext.cs backend/Ecommerce/Authorization/PermissionKeys.cs backend/Ecommerce/Migrations backend/Ecommerce.Tests/Entities/SliderModelTests.cs
git commit -m "Add Slider entity, AddSliders migration, and the sliders.view permission key"
```

---

## Task 8: `ISliderService` / `SliderService`

**Files:**
- Create: `backend/Ecommerce/Contracts/Sliders/SliderResponse.cs`
- Create: `backend/Ecommerce/Contracts/Sliders/SliderRequest.cs`
- Create: `backend/Ecommerce/Errors/SliderErrors.cs`
- Create: `backend/Ecommerce/Services/ISliderService.cs`
- Create: `backend/Ecommerce/Services/SliderService.cs`
- Test: `backend/Ecommerce.Tests/Services/SliderServiceTests.cs`

**Interfaces:**
- Consumes: `Slider` (Task 7); `Ecommerce.Storage.IFileStorage.SaveAsync(IFormFile, string, CancellationToken)` (Plan 2A); `Ecommerce.Tests.StubFileStorage` and `Ecommerce.Tests.TestFiles.Image(...)` (Task 1).
- Produces:
  ```csharp
  public interface ISliderService
  {
      Task<Result<IEnumerable<SliderResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
      Task<Result<IEnumerable<SliderResponse>>> GetActiveAsync(CancellationToken cancellationToken = default);
      Task<Result<SliderResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
      Task<Result<SliderResponse>> CreateAsync(SliderRequest request, CancellationToken cancellationToken = default);
      Task<Result<SliderResponse>> UpdateAsync(long id, SliderRequest request, CancellationToken cancellationToken = default);
      Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default);
      Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
  }
  ```
- Produces: `SliderResponse(long Id, string Title, string Image, string? Link, int? Sort, bool Status, DateTime? StartsOn, DateTime? EndsOn)`; `SliderRequest(string Title, string? Image, string? Link, int? Sort, bool Status, DateTime? StartsOn, DateTime? EndsOn, IFormFile? ImageFile = null)` — Task 9 binds the request with `[FromForm]` and Task 10 posts a `FormData` whose field names match these property names.
- Produces: `SliderErrors.SliderNotFound` (`"Slider.NotFound"`), `SliderErrors.ImageRequired` (`"Slider.ImageRequired"`), `SliderErrors.InvalidSchedule` (`"Slider.InvalidSchedule"`).

- [ ] **Step 1: Write the contracts**

```csharp
// backend/Ecommerce/Contracts/Sliders/SliderResponse.cs
namespace Ecommerce.Contracts.Sliders;

public record SliderResponse(
    long Id,
    string Title,
    string Image,
    string? Link,
    int? Sort,
    bool Status,
    DateTime? StartsOn,
    DateTime? EndsOn);
```

```csharp
// backend/Ecommerce/Contracts/Sliders/SliderRequest.cs
namespace Ecommerce.Contracts.Sliders;

// ImageFile is the multipart upload; Image is the already-stored path.
// If ImageFile is present it wins and its saved path replaces Image;
// otherwise Image is kept as-is, which is how "leave the current image
// alone" is expressed on an update.
public record SliderRequest(
    string Title,
    string? Image,
    string? Link,
    int? Sort,
    bool Status,
    DateTime? StartsOn,
    DateTime? EndsOn,
    IFormFile? ImageFile = null);

public class SliderRequestValidator : AbstractValidator<SliderRequest>
{
    public SliderRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Link).MaximumLength(500);
    }
}
```

- [ ] **Step 2: Write the errors**

```csharp
// backend/Ecommerce/Errors/SliderErrors.cs
namespace Ecommerce.Errors;

public static class SliderErrors
{
    public static readonly Error SliderNotFound = new("Slider.NotFound", "No slider was found with the given ID");
    public static readonly Error ImageRequired = new("Slider.ImageRequired", "A slider needs an image");
    public static readonly Error InvalidSchedule = new("Slider.InvalidSchedule", "The end date must be after the start date");
}
```

- [ ] **Step 3: Write the failing tests**

```csharp
// backend/Ecommerce.Tests/Services/SliderServiceTests.cs
using Ecommerce.Contracts.Sliders;
using Ecommerce.Entities;
using Ecommerce.Errors;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class SliderServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static SliderRequest Request(
        string title = "Summer sale",
        string? image = null,
        int? sort = 1,
        bool status = true,
        DateTime? startsOn = null,
        DateTime? endsOn = null,
        IFormFile? imageFile = null) =>
        new(title, image, "/products", sort, status, startsOn, endsOn, imageFile);

    [Fact]
    public async Task CreateAsync_saves_the_uploaded_image_and_returns_its_path()
    {
        await using var context = CreateContext();
        var storage = new StubFileStorage("/uploads/sliders/hero.jpg");
        var service = new SliderService(context, storage);

        var result = await service.CreateAsync(Request(imageFile: TestFiles.Image()));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/sliders/hero.jpg", result.Value.Image);
        Assert.Equal("sliders", storage.LastModule);
    }

    [Fact]
    public async Task CreateAsync_fails_when_no_image_is_supplied()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());

        var result = await service.CreateAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("Slider.ImageRequired", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_fails_when_the_end_date_precedes_the_start_date()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());

        var result = await service.CreateAsync(Request(
            image: "/uploads/sliders/hero.jpg",
            startsOn: new DateTime(2026, 8, 10),
            endsOn: new DateTime(2026, 8, 1)));

        Assert.False(result.IsSuccess);
        Assert.Equal("Slider.InvalidSchedule", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_propagates_a_file_storage_failure()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage(failWith: FileErrors.TooLarge));

        var result = await service.CreateAsync(Request(imageFile: TestFiles.Image()));

        Assert.False(result.IsSuccess);
        Assert.Equal("File.TooLarge", result.Error.Code);
        Assert.False(await context.Sliders.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_keeps_the_existing_image_when_no_new_file_is_uploaded()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        var created = (await service.CreateAsync(Request(image: "/uploads/sliders/original.jpg"))).Value;

        var result = await service.UpdateAsync(created.Id, Request(title: "Renamed"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value.Title);
        Assert.Equal("/uploads/sliders/original.jpg", result.Value.Image);
    }

    [Fact]
    public async Task GetActiveAsync_excludes_inactive_sliders()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        await service.CreateAsync(Request(title: "Live", image: "/uploads/sliders/a.jpg", status: true));
        await service.CreateAsync(Request(title: "Draft", image: "/uploads/sliders/b.jpg", status: false));

        var result = await service.GetActiveAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Live" }, result.Value.Select(x => x.Title));
    }

    [Fact]
    public async Task GetActiveAsync_excludes_sliders_outside_their_schedule_window()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        var now = DateTime.UtcNow;

        await service.CreateAsync(Request(title: "Always on", image: "/uploads/sliders/a.jpg"));
        await service.CreateAsync(Request(title: "Running", image: "/uploads/sliders/b.jpg", startsOn: now.AddDays(-1), endsOn: now.AddDays(1)));
        await service.CreateAsync(Request(title: "Not started", image: "/uploads/sliders/c.jpg", startsOn: now.AddDays(5)));
        await service.CreateAsync(Request(title: "Expired", image: "/uploads/sliders/d.jpg", endsOn: now.AddDays(-5)));

        var result = await service.GetActiveAsync();

        Assert.True(result.IsSuccess);
        var titles = result.Value.Select(x => x.Title).ToList();
        Assert.Contains("Always on", titles);
        Assert.Contains("Running", titles);
        Assert.DoesNotContain("Not started", titles);
        Assert.DoesNotContain("Expired", titles);
    }

    [Fact]
    public async Task GetActiveAsync_orders_by_sort()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        await service.CreateAsync(Request(title: "Third", image: "/uploads/sliders/c.jpg", sort: 3));
        await service.CreateAsync(Request(title: "First", image: "/uploads/sliders/a.jpg", sort: 1));
        await service.CreateAsync(Request(title: "Second", image: "/uploads/sliders/b.jpg", sort: 2));

        var result = await service.GetActiveAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "First", "Second", "Third" }, result.Value.Select(x => x.Title));
    }

    [Fact]
    public async Task ToggleStatusAsync_flips_the_status()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        var created = (await service.CreateAsync(Request(image: "/uploads/sliders/a.jpg", status: true))).Value;

        var result = await service.ToggleStatusAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.False((await service.GetByIdAsync(created.Id)).Value.Status);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_slider_from_ordinary_queries()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        var created = (await service.CreateAsync(Request(image: "/uploads/sliders/a.jpg"))).Value;

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Sliders.AnyAsync(x => x.Id == created.Id));
    }

    [Fact]
    public async Task GetByIdAsync_fails_for_an_unknown_slider()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());

        var result = await service.GetByIdAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Equal("Slider.NotFound", result.Error.Code);
    }
}
```

- [ ] **Step 4: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter SliderServiceTests
```

Expected: FAIL — `ISliderService`/`SliderService` and the `Ecommerce.Contracts.Sliders` namespace don't exist yet.

- [ ] **Step 5: Write `ISliderService`**

```csharp
// backend/Ecommerce/Services/ISliderService.cs
using Ecommerce.Contracts.Sliders;

namespace Ecommerce.Services;

public interface ISliderService
{
    // Admin view: every non-deleted slider, whatever its status or schedule.
    Task<Result<IEnumerable<SliderResponse>>> GetAllAsync(CancellationToken cancellationToken = default);

    // Storefront view: active AND currently within its schedule window, ordered by Sort.
    Task<Result<IEnumerable<SliderResponse>>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<Result<SliderResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<SliderResponse>> CreateAsync(SliderRequest request, CancellationToken cancellationToken = default);
    Task<Result<SliderResponse>> UpdateAsync(long id, SliderRequest request, CancellationToken cancellationToken = default);
    Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 6: Write `SliderService`**

```csharp
// backend/Ecommerce/Services/SliderService.cs
using Ecommerce.Contracts.Sliders;
using Ecommerce.Storage;

namespace Ecommerce.Services;

public class SliderService(ApplicationDbContext context, IFileStorage fileStorage) : ISliderService
{
    private const string StorageModule = "sliders";

    private readonly ApplicationDbContext _context = context;
    private readonly IFileStorage _fileStorage = fileStorage;

    public async Task<Result<IEnumerable<SliderResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sliders = await _context.Sliders.AsNoTracking()
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<SliderResponse>>(sliders.Select(MapSlider).ToList());
    }

    public async Task<Result<IEnumerable<SliderResponse>>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        // Scheduling is evaluated here so the storefront needs no date logic.
        var now = DateTime.UtcNow;

        var sliders = await _context.Sliders.AsNoTracking()
            .Where(x => x.Status
                        && (x.StartsOn == null || x.StartsOn <= now)
                        && (x.EndsOn == null || x.EndsOn >= now))
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<SliderResponse>>(sliders.Select(MapSlider).ToList());
    }

    public async Task<Result<SliderResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var slider = await _context.Sliders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return slider is null
            ? Result.Failure<SliderResponse>(SliderErrors.SliderNotFound)
            : Result.Success(MapSlider(slider));
    }

    public async Task<Result<SliderResponse>> CreateAsync(SliderRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsScheduleValid(request))
            return Result.Failure<SliderResponse>(SliderErrors.InvalidSchedule);

        var imageResult = await ResolveImageAsync(request, currentImage: null, cancellationToken);
        if (!imageResult.IsSuccess)
            return Result.Failure<SliderResponse>(imageResult.Error);

        if (string.IsNullOrWhiteSpace(imageResult.Value))
            return Result.Failure<SliderResponse>(SliderErrors.ImageRequired);

        var slider = new Slider
        {
            Title = request.Title,
            Image = imageResult.Value!,
            Link = request.Link,
            Sort = request.Sort,
            Status = request.Status,
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
        };

        await _context.Sliders.AddAsync(slider, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapSlider(slider));
    }

    public async Task<Result<SliderResponse>> UpdateAsync(long id, SliderRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsScheduleValid(request))
            return Result.Failure<SliderResponse>(SliderErrors.InvalidSchedule);

        var slider = await _context.Sliders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (slider is null)
            return Result.Failure<SliderResponse>(SliderErrors.SliderNotFound);

        var imageResult = await ResolveImageAsync(request, slider.Image, cancellationToken);
        if (!imageResult.IsSuccess)
            return Result.Failure<SliderResponse>(imageResult.Error);

        if (string.IsNullOrWhiteSpace(imageResult.Value))
            return Result.Failure<SliderResponse>(SliderErrors.ImageRequired);

        slider.Title = request.Title;
        slider.Image = imageResult.Value!;
        slider.Link = request.Link;
        slider.Sort = request.Sort;
        slider.Status = request.Status;
        slider.StartsOn = request.StartsOn;
        slider.EndsOn = request.EndsOn;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(MapSlider(slider));
    }

    public async Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default)
    {
        var slider = await _context.Sliders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (slider is null)
            return Result.Failure(SliderErrors.SliderNotFound);

        slider.Status = !slider.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var slider = await _context.Sliders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (slider is null)
            return Result.Failure(SliderErrors.SliderNotFound);

        // The DbContext hook turns this into a soft delete.
        _context.Remove(slider);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static bool IsScheduleValid(SliderRequest request) =>
        request.StartsOn is null || request.EndsOn is null || request.EndsOn >= request.StartsOn;

    // ImageFile wins if present; otherwise a non-empty Image string wins;
    // otherwise the current stored path is kept unchanged.
    private async Task<Result<string?>> ResolveImageAsync(SliderRequest request, string? currentImage, CancellationToken cancellationToken)
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

    private static SliderResponse MapSlider(Slider slider) => new(
        slider.Id, slider.Title, slider.Image, slider.Link, slider.Sort, slider.Status, slider.StartsOn, slider.EndsOn);
}
```

- [ ] **Step 7: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter SliderServiceTests
```

Expected: 11 passed.

- [ ] **Step 8: Commit**

```bash
git add backend/Ecommerce/Contracts/Sliders backend/Ecommerce/Errors/SliderErrors.cs backend/Ecommerce/Services/ISliderService.cs backend/Ecommerce/Services/SliderService.cs backend/Ecommerce.Tests/Services/SliderServiceTests.cs
git commit -m "Add ISliderService/SliderService with image upload and server-side scheduling"
```

---

## Task 9: `AdminSlidersController` + public `SlidersController`

**Files:**
- Create: `backend/Ecommerce/Controllers/AdminSlidersController.cs`
- Create: `backend/Ecommerce/Controllers/SlidersController.cs`
- Modify: `backend/Ecommerce/DependacyInjection.cs` (register `ISliderService`)

**Interfaces:**
- Consumes: `ISliderService` (`GetAllAsync`, `GetActiveAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `ToggleStatusAsync`, `DeleteAsync`) and `SliderRequest`/`SliderResponse` (Task 8); `PermissionKeys.SlidersView` / `PermissionKeys.SlidersManage` (Task 7); `AdminAuthDefaults.Scheme`.
- Produces: `GET|POST api/Admin/Sliders`, `GET|PUT|DELETE api/Admin/Sliders/{id}`, `PUT api/Admin/Sliders/{id}/toggleStatus` — Task 10's `SliderServices` calls all six. Produces `GET api/Sliders` (unauthenticated, active-and-scheduled only) for the storefront; the storefront carousel component itself is out of scope for this phase.

- [ ] **Step 1: Write the admin controller**

```csharp
// backend/Ecommerce/Controllers/AdminSlidersController.cs
using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Sliders;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Sliders")]
[ApiController]
public class AdminSlidersController(ISliderService sliderService) : ControllerBase
{
    private readonly ISliderService _sliderService = sliderService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.SlidersView)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _sliderService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<SliderResponse>>(StatusCodes.Status200OK, "Sliders loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionKeys.SlidersView)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _sliderService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Slider not found."));

        return Ok(new ApiResponse<SliderResponse>(StatusCodes.Status200OK, "Slider loaded.", result.Value));
    }

    [HttpPost("")]
    [HasPermission(PermissionKeys.SlidersManage)]
    public async Task<IActionResult> CreateAsync([FromForm] SliderRequest request, CancellationToken cancellationToken)
    {
        var result = await _sliderService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create slider."));

        var response = new ApiResponse<SliderResponse>(StatusCodes.Status201Created, "Slider created.", result.Value);
        return Created($"/api/Admin/Sliders/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionKeys.SlidersManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromForm] SliderRequest request, CancellationToken cancellationToken)
    {
        var result = await _sliderService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update slider."));

        return Ok(new ApiResponse<SliderResponse>(StatusCodes.Status200OK, "Slider updated.", result.Value));
    }

    [HttpPut("{id:long}/toggleStatus")]
    [HasPermission(PermissionKeys.SlidersManage)]
    public async Task<IActionResult> ToggleStatusAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _sliderService.ToggleStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not toggle slider status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Slider status toggled."));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionKeys.SlidersManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _sliderService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete slider."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Slider deleted."));
    }
}
```

- [ ] **Step 2: Write the public controller**

```csharp
// backend/Ecommerce/Controllers/SlidersController.cs
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Sliders;

namespace Ecommerce.Controllers;

// Storefront-facing, unauthenticated, read-only. Returns only sliders that are
// active AND currently inside their StartsOn/EndsOn window, ordered by Sort —
// the schedule is evaluated server-side so the client needs no date logic.
[Route("api/[controller]")]
[ApiController]
public class SlidersController(ISliderService sliderService) : ControllerBase
{
    private readonly ISliderService _sliderService = sliderService;

    [HttpGet("")]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _sliderService.GetActiveAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<SliderResponse>>(StatusCodes.Status200OK, "Sliders loaded.", result.Value));
    }
}
```

- [ ] **Step 3: Register the service**

In `backend/Ecommerce/DependacyInjection.cs`, inside `AddDependancies`, add after `services.AddScoped<IClientService, ClientService>();`:

```csharp
            services.AddScoped<ISliderService, SliderService>();
```

- [ ] **Step 4: Build and run the whole suite**

```powershell
dotnet build Ecommerce.slnx
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: 0 build errors, all tests passing.

- [ ] **Step 5: Manually verify the endpoints**

```powershell
dotnet run --project backend/Ecommerce
```

Log in as `admin.tester@example.com` / `AdminTester@123` for a token, then:

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Sliders -H "Authorization: Bearer <token>" -F "Title=Summer sale" -F "Link=/products" -F "Sort=1" -F "Status=true" -F "ImageFile=@C:\path\to\banner.jpg"
curl.exe -k https://localhost:7297/api/Admin/Sliders -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Sliders
curl.exe -k https://localhost:7297/api/Admin/Sliders
```

Expected: `201` with an `image` value of `/uploads/sliders/<guid>.jpg`; the admin list returns it; the public list returns it too; the unauthenticated **admin** call returns `401`.

Then confirm the scheduling and status filters actually apply to the public endpoint:

```bash
curl.exe -k -X PUT https://localhost:7297/api/Admin/Sliders/<id>/toggleStatus -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Sliders
```

Expected: the slider disappears from `GET /api/Sliders` while still appearing in `GET /api/Admin/Sliders`. Toggle it back, then `PUT` it with `EndsOn` set to yesterday and confirm it drops out of the public list again.

Finally, open `https://localhost:7297/uploads/sliders/<guid>.jpg` in a browser and confirm the uploaded file is served by `UseStaticFiles()`.

- [ ] **Step 6: Commit**

```bash
git add backend/Ecommerce/Controllers/AdminSlidersController.cs backend/Ecommerce/Controllers/SlidersController.cs backend/Ecommerce/DependacyInjection.cs
git commit -m "Add AdminSlidersController and the public read-only SlidersController"
```

---

## Task 10: Sliders admin page

**Files:**
- Create: `frontend/src/app/admin/shared/interface/slider-interfaces.ts`
- Create: `frontend/src/app/admin/core/services/slider-services.ts`
- Create: `frontend/src/app/admin/features/pages/sliders/sliders.ts`
- Create: `frontend/src/app/admin/features/pages/sliders/sliders.html`
- Create: `frontend/src/app/admin/features/pages/sliders/sliders.scss`
- Modify: `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts` (add a `NAV_ITEMS` entry)
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.routes.server.ts`

**Interfaces:**
- Consumes: `GET|POST api/Admin/Sliders`, `GET|PUT|DELETE api/Admin/Sliders/{id}`, `PUT api/Admin/Sliders/{id}/toggleStatus` (Task 9); `AdminApiEnvelope<T>` and `AdminAuthServices.hasPermission(key)` (Phase 1); `adminPermissionGuard('sliders.view')` (Phase 1, key added in Task 7).
- Produces: `SliderInterface`, `SliderServices`, and the component class `SlidersComponent` — `app.routes.ts` imports it under that exact name.

- [ ] **Step 1: Write the interfaces**

```typescript
// frontend/src/app/admin/shared/interface/slider-interfaces.ts
export interface SliderInterface {
  id: number;
  title: string;
  image: string;
  link?: string | null;
  sort?: number | null;
  status: boolean;
  startsOn?: string | null;
  endsOn?: string | null;
}
```

- [ ] **Step 2: Write `SliderServices`**

```typescript
// frontend/src/app/admin/core/services/slider-services.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { SliderInterface } from '../../shared/interface/slider-interfaces';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class SliderServices {
  private http = inject(HttpClient);

  getSliders(): Observable<SliderInterface[]> {
    return this.http.get<AdminApiEnvelope<SliderInterface[]>>('/Admin/Sliders').pipe(map(response => response.data));
  }

  getSlider(id: number): Observable<SliderInterface> {
    return this.http.get<AdminApiEnvelope<SliderInterface>>(`/Admin/Sliders/${id}`).pipe(map(response => response.data));
  }

  // Sliders are posted as multipart/form-data because the request carries an
  // optional ImageFile. Do not set Content-Type — the browser adds the boundary.
  createSlider(payload: FormData): Observable<SliderInterface> {
    return this.http.post<AdminApiEnvelope<SliderInterface>>('/Admin/Sliders', payload).pipe(map(response => response.data));
  }

  updateSlider(id: number, payload: FormData): Observable<SliderInterface> {
    return this.http.put<AdminApiEnvelope<SliderInterface>>(`/Admin/Sliders/${id}`, payload).pipe(map(response => response.data));
  }

  toggleStatus(id: number): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Sliders/${id}/toggleStatus`, {}).pipe(map(() => undefined));
  }

  deleteSlider(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Sliders/${id}`).pipe(map(() => undefined));
  }
}
```

- [ ] **Step 3: Write the component**

```typescript
// frontend/src/app/admin/features/pages/sliders/sliders.ts
import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { SliderServices } from '../../../core/services/slider-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { SliderInterface } from '../../../shared/interface/slider-interfaces';
import { Environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-admin-sliders',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './sliders.html',
  styleUrl: './sliders.scss',
})
export class SlidersComponent {
  private sliderService = inject(SliderServices);
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  // Uploaded images are served by the API host, not the Angular dev server.
  private readonly assetOrigin = Environment.apiUrl.replace(/\/api\/?$/, '');

  sliders = signal<SliderInterface[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  selectedFile = signal<File | null>(null);
  existingImage = signal<string | null>(null);

  canManage = () => this.auth.hasPermission('sliders.manage');

  form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    link: [''],
    sort: [0],
    status: [true],
    startsOn: [''],
    endsOn: [''],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.sliderService.getSliders().subscribe({
      next: data => {
        this.sliders.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  imageUrl(path?: string | null): string {
    if (!path) return '';
    return /^https?:\/\//i.test(path) ? path : `${this.assetOrigin}${path}`;
  }

  // <input type="datetime-local"> wants "YYYY-MM-DDTHH:mm"; the API returns
  // a full ISO string, so trim it (and hand back '' for null).
  private toLocalInput(value?: string | null): string {
    return value ? value.slice(0, 16) : '';
  }

  startAdd(): void {
    this.editingId.set(null);
    this.selectedFile.set(null);
    this.existingImage.set(null);
    this.form.reset({ title: '', link: '', sort: 0, status: true, startsOn: '', endsOn: '' });
    this.showForm.set(true);
  }

  startEdit(slider: SliderInterface): void {
    this.editingId.set(slider.id);
    this.selectedFile.set(null);
    this.existingImage.set(slider.image ?? null);
    this.form.reset({
      title: slider.title,
      link: slider.link ?? '',
      sort: slider.sort ?? 0,
      status: slider.status,
      startsOn: this.toLocalInput(slider.startsOn),
      endsOn: this.toLocalInput(slider.endsOn),
    });
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  private buildFormData(): FormData {
    const raw = this.form.getRawValue();
    const payload = new FormData();

    payload.append('Title', raw.title);
    payload.append('Status', String(raw.status));
    payload.append('Sort', String(raw.sort ?? 0));

    if (raw.link) payload.append('Link', raw.link);
    if (raw.startsOn) payload.append('StartsOn', raw.startsOn);
    if (raw.endsOn) payload.append('EndsOn', raw.endsOn);

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

    if (!this.editingId() && !this.selectedFile()) {
      this.error.set('Pick an image — a new slider needs one.');
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const editingId = this.editingId();
    const payload = this.buildFormData();
    const request$ = editingId
      ? this.sliderService.updateSlider(editingId, payload)
      : this.sliderService.createSlider(payload);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this slider. Check the end date is after the start date and the image is a JPG/PNG/WebP under 2 MB.');
      },
    });
  }

  toggleStatus(slider: SliderInterface): void {
    this.busyId.set(slider.id);
    this.sliderService.toggleStatus(slider.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(slider: SliderInterface): void {
    this.busyId.set(slider.id);
    this.sliderService.deleteSlider(slider.id).subscribe({
      next: () => {
        this.sliders.update(items => items.filter(s => s.id !== slider.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
```

- [ ] **Step 4: Write the template**

```html
<!-- frontend/src/app/admin/features/pages/sliders/sliders.html -->
<div class="panel-header">
  <div>
    <h1 class="page-title">Sliders</h1>
    <p class="page-subtitle">Homepage banners. Scheduling is applied on the storefront automatically.</p>
  </div>
  @if (!showForm() && canManage()) {
    <button type="button" class="add-btn" (click)="startAdd()">+ Add Slider</button>
  }
</div>

@if (loading()) {
  <div class="state-message">Loading sliders…</div>
} @else if (!showForm()) {
  <table class="data-table">
    <thead>
      <tr>
        <th>Image</th>
        <th>Title</th>
        <th>Link</th>
        <th>Sort</th>
        <th>Schedule</th>
        <th>Status</th>
        @if (canManage()) { <th>Actions</th> }
      </tr>
    </thead>
    <tbody>
      @for (slider of sliders(); track slider.id) {
        <tr>
          <td><img class="thumb" [src]="imageUrl(slider.image)" [alt]="slider.title"></td>
          <td>{{ slider.title }}</td>
          <td class="muted">{{ slider.link || '—' }}</td>
          <td>{{ slider.sort ?? '—' }}</td>
          <td class="muted">
            {{ slider.startsOn ? (slider.startsOn | date:'mediumDate') : 'Always' }}
            →
            {{ slider.endsOn ? (slider.endsOn | date:'mediumDate') : 'Always' }}
          </td>
          <td>
            <span class="pill" [class.pill-off]="!slider.status">{{ slider.status ? 'Active' : 'Hidden' }}</span>
          </td>
          @if (canManage()) {
            <td class="actions">
              <button type="button" (click)="startEdit(slider)">Edit</button>
              <button type="button" [disabled]="busyId() === slider.id" (click)="toggleStatus(slider)">Toggle</button>
              <button type="button" class="danger" [disabled]="busyId() === slider.id" (click)="remove(slider)">Delete</button>
            </td>
          }
        </tr>
      } @empty {
        <tr><td colspan="7" class="state-message">No sliders yet.</td></tr>
      }
    </tbody>
  </table>
}

@if (showForm()) {
  @if (error()) {
    <div class="alert-error">{{ error() }}</div>
  }

  <form [formGroup]="form" (ngSubmit)="save()" class="slider-form">
    <div class="field-group">
      <label>Title</label>
      <input formControlName="title" type="text" class="form-control">
    </div>

    <div class="field-group">
      <label>Image</label>
      @if (existingImage()) {
        <img class="preview" [src]="imageUrl(existingImage())" alt="Current image">
      }
      <input type="file" accept="image/*" class="form-control" (change)="onFileSelected($event)">
      <small class="muted">
        JPG, PNG or WebP, up to 2 MB.
        {{ editingId() ? 'Leave empty to keep the current image.' : 'Required for a new slider.' }}
      </small>
    </div>

    <div class="field-row">
      <div class="field-group">
        <label>Link</label>
        <input formControlName="link" type="text" class="form-control" placeholder="/products">
      </div>
      <div class="field-group">
        <label>Sort</label>
        <input formControlName="sort" type="number" class="form-control">
      </div>
    </div>

    <div class="field-row">
      <div class="field-group">
        <label>Starts on</label>
        <input formControlName="startsOn" type="datetime-local" class="form-control">
      </div>
      <div class="field-group">
        <label>Ends on</label>
        <input formControlName="endsOn" type="datetime-local" class="form-control">
      </div>
    </div>
    <small class="muted">Leave both empty to show the slider indefinitely.</small>

    <label class="checkbox-field"><input formControlName="status" type="checkbox"> Active</label>

    <div class="form-actions">
      <button type="submit" class="save-btn" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save Slider' }}</button>
      <button type="button" class="cancel-btn" (click)="cancel()">Cancel</button>
    </div>
  </form>
}
```

- [ ] **Step 5: Write the styles**

```scss
// frontend/src/app/admin/features/pages/sliders/sliders.scss
@import '../../../shared/scss/variables';

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 1.5rem;
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

.cancel-btn {
  background: transparent;
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.65rem 1.1rem;
  font-weight: 600;
  cursor: pointer;
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
  width: 96px;
  height: 48px;
  object-fit: cover;
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.04);
}

.preview {
  width: 240px;
  height: 120px;
  object-fit: cover;
  border-radius: 10px;
  margin-bottom: 0.5rem;
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

.slider-form {
  background: #fff;
  border-radius: $admin-radius;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 640px;
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

In `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts`, add one entry to `NAV_ITEMS` after Clients. The finished array should read:

```typescript
const NAV_ITEMS: AdminNavItem[] = [
  { label: 'Dashboard', path: '.', icon: 'bi-grid-1x2-fill', permission: 'dashboard.view' },
  { label: 'Categories', path: 'categories', icon: 'bi-diagram-3-fill', permission: 'categories.view' },
  { label: 'Clients', path: 'clients', icon: 'bi-person-lines-fill', permission: 'clients.view' },
  { label: 'Sliders', path: 'sliders', icon: 'bi-images', permission: 'sliders.view' },
  { label: 'Roles', path: 'roles', icon: 'bi-shield-lock-fill', permission: 'roles.manage' },
  { label: 'Admins', path: 'admins', icon: 'bi-people-fill', permission: 'admins.manage' },
];
```

- [ ] **Step 7: Add the route**

In `frontend/src/app/app.routes.ts`, add the import:

```typescript
import { SlidersComponent } from './admin/features/pages/sliders/sliders';
```

and add the child route inside the `path: 'admin'` block, after the clients entry:

```typescript
        { path: 'sliders', component: SlidersComponent, canActivate: [adminPermissionGuard('sliders.view')], title: 'Sliders' },
```

- [ ] **Step 8: Add the server render mode**

In `frontend/src/app/app.routes.server.ts`, add alongside the other admin entries (before the `'**'` catch-all):

```typescript
  {
    path: 'admin/sliders',
    renderMode: RenderMode.Client
  },
```

- [ ] **Step 9: Type-check**

```powershell
npx tsc --noEmit -p frontend/tsconfig.app.json
```

Expected: 0 errors.

- [ ] **Step 10: Manually verify**

With the backend and frontend running, logged in at `http://localhost:4200/admin/auth/login` as `admin.tester@example.com` / `AdminTester@123`:

1. A **Sliders** item appears in the sidebar; clicking it opens `/admin/sliders`. (If it does **not** appear, the backend has not been restarted since Task 7 — `AdminDataSeeder` needs one dev run to seed `sliders.view` onto Super Admin, and the admin needs to log out and back in to get a token carrying the new claim.)
2. Click **+ Add Slider**, enter Title `Summer sale`, Link `/products`, Sort `1`, pick a wide JPG/PNG under 2 MB, leave both dates empty, save. The row appears with a visible thumbnail and a Schedule column reading `Always → Always`.
3. Click **+ Add Slider** again and try to save with no image — the inline error `Pick an image — a new slider needs one.` appears and nothing is posted.
4. Click **Edit** on `Summer sale`, change only the Sort to `2` and save. The thumbnail is unchanged (the `Image` path round-tripped).
5. Click **Edit** again, set **Starts on** to tomorrow, save. Open `https://localhost:7297/api/Sliders` in a browser — the slider is **absent** from the public list, but still present in the admin table. Clear the date and confirm it reappears publicly.
6. Set **Ends on** to a date *before* **Starts on** and save — the inline error about the end date appears (the backend rejected it with `Slider.InvalidSchedule`).
7. Click **Toggle** — the Status pill flips to `Hidden` and the slider drops out of `https://localhost:7297/api/Sliders`. Toggle it back.
8. Delete the slider.
9. Create a role with **only** `sliders.view`, assign it to a second admin, log in as them: the Sliders page loads read-only — no `+ Add Slider`, no Edit/Toggle/Delete.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/app/admin/shared/interface/slider-interfaces.ts frontend/src/app/admin/core/services/slider-services.ts frontend/src/app/admin/features/pages/sliders frontend/src/app/admin/features/layouts/main-layout/main-layout.ts frontend/src/app/app.routes.ts frontend/src/app/app.routes.server.ts
git commit -m "Add Sliders admin page with image upload and schedule inputs"
```

---

## Plan-level final check

Once all 10 tasks are done:

- [ ] `dotnet test backend/Ecommerce.Tests/Ecommerce.Tests.csproj` — all passing, including the 7 `CategoryServiceTests`, 8 `ClientServiceTests`, 2 `SliderModelTests` and 11 `SliderServiceTests` added by this plan, plus everything Phase 1 and Plan 2A contributed.
- [ ] `dotnet build backend/Ecommerce.slnx` — 0 errors.
- [ ] `npx tsc --noEmit -p frontend/tsconfig.app.json` — 0 errors.
- [ ] `npm run build` from `frontend/` — completes. The prerender step must not attempt to render `/admin/categories`, `/admin/clients` or `/admin/sliders` (all three are `RenderMode.Client`).
- [ ] **Design-doc coverage sweep.** Confirm each of these is true in the running app:
  - Categories: `AdminCategoriesController` at `api/Admin/Categories` gated `categories.view` (reads) / `categories.manage` (writes); image upload through `IFileStorage` with module `"categories"`; public `CategoriesController` reduced to `GET ""` / `GET "{id}"`, both `ApiResponse<T>`-wrapped; admin UI has a flat table with a Parent column **and** a "Show tree" expand/collapse toggle plus a parent-picker in the form.
  - Clients: `AdminClientsController` at `api/Admin/Clients`; list with search + paging; detail with order count and lifetime total; edit of first/last name, email and phone; enable/disable via Identity lockout (`LockoutEnabled = true` + `LockoutEnd = DateTimeOffset.MaxValue` to disable, `LockoutEnd = null` to enable) surfaced as `isActive`; soft delete. Every email mutation goes through `UserManager.SetEmailAsync`/`SetUserNameAsync`.
  - Sliders: `Slider` entity with `Title`, `Image`, `Link`, `Sort`, `Status`, `StartsOn`, `EndsOn` + audit base; `sliders.view` added to `PermissionKeys.Catalog` and seeded with no migration; `AdminSlidersController` full CRUD + `toggleStatus`; public `GET api/Sliders` returning only `Status == true` and in-window sliders ordered by `Sort`; admin UI with image upload and start/end scheduling.
- [ ] **Full manual walkthrough:** admin login → Categories (create parent + child, tree toggle, edit, delete) → Clients (search, view detail, edit email, disable, re-enable, delete) → Sliders (create with image, schedule out of window, confirm it vanishes from `api/Sliders`, toggle, delete) → create a view-only role covering all three modules, assign it to a second admin, log in as them and confirm all three pages render read-only.
- [ ] **Storefront regression check:** `/home`, `/categories`, `/categories/:id`, `/products`, `/cart`, `/checkout` and `/account/**` all still work. The two files in this plan that customer flows also depend on are `CategoriesController` (writes removed — reads unchanged) and `ClientService`'s use of `UserManager` (a customer whose email an admin edited must still be able to log in with the new address).
- [ ] Confirm nothing in this plan set `CreatedById`/`UpdatedById`/`IsDeleted` by hand or threaded an `adminId` through a service — all of it is Plan 2A's `SaveChangesAsync` hook:

  ```powershell
  rg -n "IsDeleted\s*=|CreatedById\s*=|UpdatedById\s*=|DeletedById\s*=" backend/Ecommerce/Services backend/Ecommerce/Controllers
  ```

  Expected: no matches.
