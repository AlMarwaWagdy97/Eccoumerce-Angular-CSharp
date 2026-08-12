# Admin Dashboard Phase 2A: Audit, Soft-Delete & File Upload Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put a schema-wide admin audit trail, soft-delete, and file-upload capability in place so the Phase 2B feature plan (Categories, Clients, Sliders) and every later phase can build on them without re-migrating the same tables.

**Architecture:** Auditing is expressed as an `IAuditable` interface with an `AuditableEntity` base class implementing it — an interface is required because `ApplicationUser` already inherits `IdentityUser` and C# permits only one base class. Stamping and soft-delete are entirely centralised in `ApplicationDbContext`: the existing `SaveChangesAsync` override is corrected to write `long?` admin ids and to rewrite `EntityState.Deleted` into a soft delete, and `OnModelCreating` applies a global `!IsDeleted` query filter by reflection. Because both live in the DbContext, **no service signature changes and no existing `Remove()` call needs editing** — they become soft deletes automatically. File upload is a small `IFileStorage` abstraction with a local-disk implementation writing under `wwwroot/uploads/`.

**Tech Stack:** ASP.NET Core .NET 10, EF Core 10 (SQL Server; InMemory for tests), ASP.NET Core Identity, xUnit + Moq (`backend/Ecommerce.Tests`).

## Global Constraints

- This plan is derived from `docs/superpowers/specs/2026-08-12-admin-phase2-foundations-categories-clients-sliders-design.md`. Read the "Foundation A" and "Foundation B" sections before starting.
- **Plan 2B (`2026-08-12-admin-phase2b-categories-clients-sliders.md`) depends on this plan and assumes every task here is finished.** The names it consumes are fixed: `Ecommerce.Entities.IAuditable`, `Ecommerce.Entities.AuditableEntity`, `Ecommerce.Storage.IFileStorage.SaveAsync(IFormFile, string, CancellationToken)`, and `Ecommerce.Errors.FileErrors`. Do not rename any of them.
- Follow `backend/CLAUDE.md`: thin controllers → `Scoped` services returning `Result`/`Result<T>`, `ApiResponse<T>` envelope, per-domain `*Errors` classes, primary-constructor DI, `.AsNoTracking()` on reads, trailing `CancellationToken` on service methods.
- **`Result.IsFailure` is `internal`** and therefore invisible from the `Ecommerce.Tests` assembly. Always assert with `Assert.True(result.IsSuccess)` / `Assert.False(result.IsSuccess)`, never `IsFailure`.
- Audit FKs are **`long?` pointing at `Admin`**, never `ApplicationUser`. They are populated only for admin-authenticated requests; customer and anonymous requests leave them null.
- Soft-deleted entities are excluded from every query by a global filter. Use `.IgnoreQueryFilters()` only where deleted rows are deliberately wanted.
- Permission keys come from `PermissionKeys` constants — never inline strings in a `[HasPermission(...)]` attribute.
- Run backend tests from `backend/`: `dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj`. Run EF commands from `backend/` with `--project Ecommerce`.
- After Task 1 and before Task 2's migration is applied, the app cannot run against the real SQL Server database (entities have columns the schema lacks). Tests use the InMemory provider and are unaffected. Do not try to launch the API between those two tasks.

---

## Task 1: `IAuditable`, `AuditableEntity`, and entity adoption

**Files:**
- Modify: `backend/Ecommerce/Entities/AuditableEntity.cs` (full rewrite)
- Modify: `backend/Ecommerce/Entities/Category.cs`
- Modify: `backend/Ecommerce/Entities/Product.cs`
- Modify: `backend/Ecommerce/Entities/ProductImage.cs`
- Modify: `backend/Ecommerce/Entities/Order.cs`
- Modify: `backend/Ecommerce/Entities/OrderItem.cs`
- Modify: `backend/Ecommerce/Entities/Address.cs`
- Modify: `backend/Ecommerce/Entities/Card.cs`
- Modify: `backend/Ecommerce/Entities/Review.cs`
- Modify: `backend/Ecommerce/Entities/Admin.cs`
- Modify: `backend/Ecommerce/Entities/AdminRole.cs`
- Modify: `backend/Ecommerce/Entities/ApplicationUser.cs`
- Test: `backend/Ecommerce.Tests/Entities/AuditableEntityTests.cs`

**Interfaces:**
- Produces: `Ecommerce.Entities.IAuditable` with `long? CreatedById`, `DateTime CreatedOn`, `long? UpdatedById`, `DateTime? UpdatedOn`, `bool IsDeleted`, `DateTime? DeletedOn`, `long? DeletedById`, `Admin? CreatedBy`, `Admin? UpdatedBy`, `Admin? DeletedBy`; and `Ecommerce.Entities.AuditableEntity`, an abstract class implementing it. Every later task and all of Plan 2B depend on these exact member names.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/Ecommerce.Tests/Entities/AuditableEntityTests.cs
using Ecommerce.Entities;

namespace Ecommerce.Tests.Entities;

public class AuditableEntityTests
{
    public static TheoryData<Type> AuditedTypes =>
    [
        typeof(Category), typeof(Product), typeof(ProductImage),
        typeof(Order), typeof(OrderItem), typeof(Address), typeof(Card),
        typeof(Review), typeof(Admin), typeof(AdminRole), typeof(ApplicationUser),
    ];

    [Theory]
    [MemberData(nameof(AuditedTypes))]
    public void Audited_entities_implement_IAuditable(Type type)
    {
        Assert.True(typeof(IAuditable).IsAssignableFrom(type), $"{type.Name} must implement IAuditable");
    }

    [Fact]
    public void ApplicationUser_implements_the_interface_without_the_base_class()
    {
        // ApplicationUser already inherits IdentityUser, so it can only implement the interface.
        Assert.True(typeof(IAuditable).IsAssignableFrom(typeof(ApplicationUser)));
        Assert.False(typeof(AuditableEntity).IsAssignableFrom(typeof(ApplicationUser)));
    }

    [Fact]
    public void A_new_auditable_entity_defaults_to_not_deleted_with_a_creation_timestamp()
    {
        var category = new Category { Title = "Shoes", Slug = "shoes" };

        Assert.False(category.IsDeleted);
        Assert.Null(category.DeletedOn);
        Assert.Null(category.CreatedById);
        Assert.NotEqual(default, category.CreatedOn);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

From `backend/`:

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~AuditableEntityTests
```

Expected: compile error — `IAuditable` does not exist.

- [ ] **Step 3: Rewrite `AuditableEntity.cs`**

Replace the entire file contents:

```csharp
// backend/Ecommerce/Entities/AuditableEntity.cs
namespace Ecommerce.Entities;

// Audit + soft-delete contract. This is an interface (not just a base class) because
// ApplicationUser already inherits IdentityUser and C# allows only one base class —
// a base-class-only design could not cover customer accounts at all.
// CreatedBy/UpdatedBy/DeletedBy point at Admin, not ApplicationUser: this is an
// admin-action audit trail, so customer self-service writes leave them null.
public interface IAuditable
{
    long? CreatedById { get; set; }
    DateTime CreatedOn { get; set; }
    long? UpdatedById { get; set; }
    DateTime? UpdatedOn { get; set; }

    bool IsDeleted { get; set; }
    DateTime? DeletedOn { get; set; }
    long? DeletedById { get; set; }

    Admin? CreatedBy { get; set; }
    Admin? UpdatedBy { get; set; }
    Admin? DeletedBy { get; set; }
}

public abstract class AuditableEntity : IAuditable
{
    public long? CreatedById { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public long? UpdatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedOn { get; set; }
    public long? DeletedById { get; set; }

    public Admin? CreatedBy { get; set; }
    public Admin? UpdatedBy { get; set; }
    public Admin? DeletedBy { get; set; }
}
```

- [ ] **Step 4: Make the ten class-based entities inherit the base**

For each of these files, add `: AuditableEntity` to the class declaration. `sealed` is not an obstacle — it blocks being inherited *from*, not inheriting.

| File | Change the declaration to |
| --- | --- |
| `Entities/Category.cs` | `public sealed class Category : AuditableEntity` |
| `Entities/Product.cs` | `public class Product : AuditableEntity` |
| `Entities/ProductImage.cs` | `public sealed class ProductImage : AuditableEntity` |
| `Entities/Order.cs` | `public sealed class Order : AuditableEntity` |
| `Entities/OrderItem.cs` | `public sealed class OrderItem : AuditableEntity` |
| `Entities/Address.cs` | `public sealed class Address : AuditableEntity` |
| `Entities/Card.cs` | `public sealed class Card : AuditableEntity` |
| `Entities/Review.cs` | `public sealed class Review : AuditableEntity` |
| `Entities/Admin.cs` | `public class Admin : AuditableEntity` |
| `Entities/AdminRole.cs` | `public class AdminRole : AuditableEntity` |

- [ ] **Step 5: Delete the now-duplicate `CreatedOn` declarations**

`Order`, `Review`, and `Admin` each declare their own `CreatedOn`. Leaving them in place is member hiding and confuses EF's model building. Delete this exact line from **`Entities/Order.cs`**, **`Entities/Review.cs`**, and **`Entities/Admin.cs`**:

```csharp
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
```

The base declares the identical name, type, and default, so no consumer breaks — including the `createdOn` field the Phase 1 Admins page reads.

- [ ] **Step 6: Make `ApplicationUser` implement the interface directly**

Replace the entire file contents:

```csharp
// backend/Ecommerce/Entities/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Entities;

// Implements IAuditable rather than inheriting AuditableEntity: the single base-class
// slot is already taken by IdentityUser. The properties are duplicated deliberately.
public sealed class ApplicationUser : IdentityUser, IAuditable
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public List<RefreshToken> RefreshTokens { get; set; } = [];

    public long? CreatedById { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public long? UpdatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedOn { get; set; }
    public long? DeletedById { get; set; }

    public Admin? CreatedBy { get; set; }
    public Admin? UpdatedBy { get; set; }
    public Admin? DeletedBy { get; set; }
}
```

- [ ] **Step 7: Build and run the test to verify it passes**

From `backend/`:

```powershell
dotnet build Ecommerce.slnx
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~AuditableEntityTests
```

Expected: build succeeds, 13 tests pass. If the build reports a duplicate-member error on `CreatedOn`, Step 5 was missed for one of the three entities.

- [ ] **Step 8: Run the whole suite to confirm nothing regressed**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 9: Commit**

```bash
git add backend/Ecommerce/Entities backend/Ecommerce.Tests/Entities/AuditableEntityTests.cs
git commit -m "Add IAuditable/AuditableEntity and adopt it across the schema"
```

---

## Task 2: The audit + soft-delete migration

**Files:**
- Create: `backend/Ecommerce/Migrations/<timestamp>_AddAuditAndSoftDelete.cs` (generated)
- Modify: `backend/Ecommerce/Migrations/ApplicationDbContextModelSnapshot.cs` (generated)

**Interfaces:**
- Consumes: the entity changes from Task 1.
- Produces: a database schema carrying `CreatedById`, `CreatedOn`, `UpdatedById`, `UpdatedOn`, `IsDeleted`, `DeletedOn`, `DeletedById` on all eleven audited tables. Plan 2B's `AddSliders` migration is generated on top of this one.

- [ ] **Step 1: Generate the migration**

From `backend/`:

```powershell
dotnet ef migrations add AddAuditAndSoftDelete --project Ecommerce
```

- [ ] **Step 2: Inspect the generated migration**

Open the new `backend/Ecommerce/Migrations/<timestamp>_AddAuditAndSoftDelete.cs` and confirm:

- `AddColumn<bool>(name: "IsDeleted", ...)` appears for `Categories`, `Products`, `ProductImages`, `Orders`, `OrderItems`, `Addresses`, `Cards`, `Reviews`, `Admins`, `AdminRoles`, and `AspNetUsers` — eleven tables.
- Each of those tables also gains `CreatedById`, `CreatedOn`, `UpdatedById`, `UpdatedOn`, `DeletedOn`, `DeletedById`.
- The `CreatedById`/`UpdatedById`/`DeletedById` foreign keys all target `Admins` with `onDelete: ReferentialAction.Restrict`. (`OnModelCreating` already rewrites every cascade FK to `Restrict`, so this should be automatic — if any FK shows `Cascade`, stop and investigate before continuing.)
- `Admins` has three self-referencing FKs back to `Admins`. This is expected and legal under `Restrict`.

- [ ] **Step 3: Apply the migration**

```powershell
dotnet ef database update --project Ecommerce
```

Expected: completes without error. This step needs the SQL Server instance in the `DefaultConnection` connection string to be reachable.

- [ ] **Step 4: Verify the app still boots**

```powershell
dotnet run --project Ecommerce
```

Expected: starts and listens on `https://localhost:7297` with no model or schema errors. The dev seeders run on startup; they must complete without throwing. Stop the app with Ctrl+C.

- [ ] **Step 5: Commit**

```bash
git add backend/Ecommerce/Migrations
git commit -m "Add EF migration for audit and soft-delete columns"
```

---

## Task 3: Global soft-delete query filter

**Files:**
- Modify: `backend/Ecommerce/Presistence/ApplicationDbContext.cs` (`OnModelCreating`)
- Modify: `backend/Ecommerce/Program.cs` (`AddDbContext` call)
- Modify: `backend/Ecommerce/DependacyInjection.cs` (`AddDbContext` call)
- Test: `backend/Ecommerce.Tests/Presistence/SoftDeleteQueryFilterTests.cs`

**Interfaces:**
- Consumes: `IAuditable` from Task 1.
- Produces: every `IAuditable` entity is filtered by `!IsDeleted` in all queries. Task 4 and Plan 2B both rely on this being automatic.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/Ecommerce.Tests/Presistence/SoftDeleteQueryFilterTests.cs
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Presistence;

public class SoftDeleteQueryFilterTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    [Fact]
    public async Task Soft_deleted_rows_are_hidden_from_ordinary_queries()
    {
        await using var context = CreateContext();
        context.Categories.Add(new Category { Title = "Live", Slug = "live" });
        context.Categories.Add(new Category { Title = "Gone", Slug = "gone", IsDeleted = true });
        await context.SaveChangesAsync();

        var visible = await context.Categories.ToListAsync();

        Assert.Single(visible);
        Assert.Equal("Live", visible[0].Title);
    }

    [Fact]
    public async Task IgnoreQueryFilters_still_sees_soft_deleted_rows()
    {
        await using var context = CreateContext();
        context.Categories.Add(new Category { Title = "Gone", Slug = "gone", IsDeleted = true });
        await context.SaveChangesAsync();

        var all = await context.Categories.IgnoreQueryFilters().ToListAsync();

        Assert.Single(all);
    }

    [Fact]
    public async Task The_filter_applies_to_customer_accounts_too()
    {
        await using var context = CreateContext();
        context.Users.Add(new ApplicationUser { UserName = "a@b.com", Email = "a@b.com", IsDeleted = true });
        await context.SaveChangesAsync();

        Assert.Empty(await context.Users.ToListAsync());
        Assert.Single(await context.Users.IgnoreQueryFilters().ToListAsync());
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~SoftDeleteQueryFilterTests
```

Expected: FAIL — all three tests see the soft-deleted rows, because no filter exists yet.

- [ ] **Step 3: Apply the filter by reflection in `OnModelCreating`**

In `backend/Ecommerce/Presistence/ApplicationDbContext.cs`, add these usings at the top of the file:

```csharp
using System.Linq.Expressions;
```

Then replace the body of `OnModelCreating` with:

```csharp
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            var cascadeFKs = modelBuilder.Model
                    .GetEntityTypes()
                    .SelectMany(t => t.GetForeignKeys())
                    .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade && !fk.IsOwnership);

            foreach (var fk in cascadeFKs)
                fk.DeleteBehavior = DeleteBehavior.Restrict;

            base.OnModelCreating(modelBuilder);

            // Soft delete: hide IsDeleted rows from every query. This runs AFTER base.OnModelCreating
            // so the Identity entity types (ApplicationUser) are already registered and get filtered too.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(IAuditable).IsAssignableFrom(entityType.ClrType))
                    continue;

                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var body = Expression.Not(Expression.Property(parameter, nameof(IAuditable.IsDeleted)));
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
            }
        }
```

The loop must stay after `base.OnModelCreating(modelBuilder)` — before it, `ApplicationUser` has not been registered and would silently go unfiltered.

- [ ] **Step 4: Suppress the expected required-navigation warning**

`Favorite`, `Cart`, and `CartItem` are deliberately unfiltered but have required navigations to the filtered `Product`, which makes EF raise `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning`. It is expected here, so silence it explicitly rather than leaving noise in the build.

`AddDbContext` is unfortunately called in **two** places today (a pre-existing duplication). Update both so the behaviour is identical whichever registration wins.

In `backend/Ecommerce/Program.cs`, add the using:

```csharp
using Microsoft.EntityFrameworkCore.Diagnostics;
```

and change the registration to:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
           // Favorite/Cart/CartItem are intentionally not soft-deletable but navigate to the
           // filtered Product entity. The warning is expected, not a defect.
           .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
```

In `backend/Ecommerce/DependacyInjection.cs`, add the same using and make the identical change to the `AddDbContext` call there.

- [ ] **Step 5: Run the test to verify it passes**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~SoftDeleteQueryFilterTests
```

Expected: PASS, 3 tests.

- [ ] **Step 6: Run the whole suite**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: all tests pass. Phase 1's `RoleServiceTests`/`AdminServiceTests` must be unaffected — nothing they create is soft-deleted.

- [ ] **Step 7: Commit**

```bash
git add backend/Ecommerce/Presistence/ApplicationDbContext.cs backend/Ecommerce/Program.cs backend/Ecommerce/DependacyInjection.cs backend/Ecommerce.Tests/Presistence/SoftDeleteQueryFilterTests.cs
git commit -m "Apply a global soft-delete query filter to auditable entities"
```

---

## Task 4: Audit stamping and delete interception in `SaveChanges`

**Files:**
- Modify: `backend/Ecommerce/Presistence/ApplicationDbContext.cs` (`SaveChangesAsync`, new `SaveChanges` override, new private helpers)
- Test: `backend/Ecommerce.Tests/Presistence/AuditStampingTests.cs`

**Interfaces:**
- Consumes: `IAuditable` (Task 1), the query filter (Task 3).
- Produces: automatic stamping of `CreatedById`/`UpdatedById`/`UpdatedOn` and automatic conversion of `EntityState.Deleted` into `IsDeleted = true` + `DeletedOn` + `DeletedById`. **This is the reason no service in Plan 2B takes an `adminId` parameter and no `Remove()` call needs rewriting.**

- [ ] **Step 1: Write the failing test**

The admin JWT puts the admin's numeric id in `JwtRegisteredClaimNames.Sub`, which ASP.NET maps to `ClaimTypes.NameIdentifier`; the customer JWT puts a GUID string there. `long.TryParse` is what separates them — a GUID never parses as a long.

```csharp
// backend/Ecommerce.Tests/Presistence/AuditStampingTests.cs
using System.Security.Claims;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Presistence;

public class AuditStampingTests
{
    private static IHttpContextAccessor AccessorFor(string? nameIdentifier)
    {
        var accessor = new NoopHttpContextAccessor();
        if (nameIdentifier is null)
            return accessor;

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, nameIdentifier)], "TestAuth");
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return accessor;
    }

    private static ApplicationDbContext CreateContext(string databaseName, string? nameIdentifier) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(databaseName).Options,
        AccessorFor(nameIdentifier));

    [Fact]
    public async Task An_admin_request_stamps_CreatedById()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString(), "7");
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        var category = await context.Categories.SingleAsync();
        Assert.Equal(7, category.CreatedById);
    }

    [Fact]
    public async Task A_customer_request_leaves_the_admin_audit_columns_null()
    {
        // A customer id is a GUID string; it must never be written into an Admin foreign key.
        await using var context = CreateContext(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        var category = await context.Categories.SingleAsync();
        Assert.Null(category.CreatedById);
    }

    [Fact]
    public async Task An_anonymous_request_leaves_the_admin_audit_columns_null()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString(), null);
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        Assert.Null((await context.Categories.SingleAsync()).CreatedById);
    }

    [Fact]
    public async Task An_update_stamps_UpdatedById_and_UpdatedOn()
    {
        var database = Guid.NewGuid().ToString();
        await using var context = CreateContext(database, "7");
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        var category = await context.Categories.SingleAsync();
        category.Title = "Boots";
        await context.SaveChangesAsync();

        Assert.Equal(7, category.UpdatedById);
        Assert.NotNull(category.UpdatedOn);
    }

    [Fact]
    public async Task Remove_becomes_a_soft_delete()
    {
        var database = Guid.NewGuid().ToString();
        await using var context = CreateContext(database, "7");
        context.Categories.Add(new Category { Title = "Shoes", Slug = "shoes" });
        await context.SaveChangesAsync();

        context.Categories.Remove(await context.Categories.SingleAsync());
        await context.SaveChangesAsync();

        Assert.Empty(await context.Categories.ToListAsync());

        var deleted = await context.Categories.IgnoreQueryFilters().SingleAsync();
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedOn);
        Assert.Equal(7, deleted.DeletedById);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~AuditStampingTests
```

Expected: FAIL. `An_admin_request_stamps_CreatedById` fails because the current hook writes a `string` into what is now a `long?`, and `Remove_becomes_a_soft_delete` fails because the row is really deleted.

If instead you get a compile error on `DefaultHttpContext`, add this item group to `backend/Ecommerce.Tests/Ecommerce.Tests.csproj` and re-run:

```xml
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
```

- [ ] **Step 3: Replace the `SaveChangesAsync` override**

In `backend/Ecommerce/Presistence/ApplicationDbContext.cs`, replace the whole existing `SaveChangesAsync` override with the following four members:

```csharp
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditRules();
            return base.SaveChangesAsync(cancellationToken);
        }

        // Overridden as well so synchronous saves cannot bypass auditing or soft delete.
        public override int SaveChanges()
        {
            ApplyAuditRules();
            return base.SaveChanges();
        }

        private void ApplyAuditRules()
        {
            var adminId = CurrentAdminId();
            var now = DateTime.UtcNow;

            // Materialised first: flipping an entry's State mutates the change tracker,
            // which would invalidate a live enumeration.
            foreach (var entry in ChangeTracker.Entries<IAuditable>().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedById = adminId;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedById = adminId;
                        entry.Entity.UpdatedOn = now;
                        break;

                    case EntityState.Deleted:
                        // Soft delete: rewrite the delete into an update. Every existing
                        // Remove() call in every service becomes a soft delete for free.
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedOn = now;
                        entry.Entity.DeletedById = adminId;
                        break;
                }
            }
        }

        // The admin JWT carries the Admin's numeric id in `sub` (mapped to NameIdentifier);
        // the customer JWT carries a GUID there. Only a long is a real Admin id, so a failed
        // parse means "not an admin request" and the audit columns stay null.
        private long? CurrentAdminId()
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(value, out var adminId) ? adminId : null;
        }
```

`using System.Security.Claims;` is already present at the top of the file.

- [ ] **Step 4: Run the test to verify it passes**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~AuditStampingTests
```

Expected: PASS, 5 tests.

- [ ] **Step 5: Run the whole suite**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add backend/Ecommerce/Presistence/ApplicationDbContext.cs backend/Ecommerce.Tests/Presistence/AuditStampingTests.cs
git commit -m "Stamp admin audit fields and convert deletes to soft deletes in SaveChanges"
```

---

## Task 5: Keep registration honest about soft-deleted emails

**Files:**
- Modify: `backend/Ecommerce/Services/AuthService.cs` (`RegisterAsync`)
- Test: `backend/Ecommerce.Tests/Services/AuthServiceRegistrationTests.cs`

**Interfaces:**
- Consumes: the query filter (Task 3).
- Produces: `RegisterAsync` rejects an email belonging to a soft-deleted account with `UserErrors.DuplicatedEmail` instead of failing on the database unique index.

The filter makes `UserManager.FindByEmailAsync` skip soft-deleted users. That is exactly what we want for login, but it makes a deleted account's email *look* free at registration — the insert would then fail on the unique index with an opaque error.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/Ecommerce.Tests/Services/AuthServiceRegistrationTests.cs
using Ecommerce.Authentication;
using Ecommerce.Contracts.Authentication;
using Ecommerce.Entities;
using Ecommerce.Errors;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ecommerce.Tests.Services;

public class AuthServiceRegistrationTests
{
    private static ServiceProvider BuildProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHttpContextAccessor>(new NoopHttpContextAccessor());
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RegisterAsync_rejects_an_email_held_by_a_soft_deleted_account()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var provider = BuildProvider(databaseName);

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var deleted = new ApplicationUser
        {
            UserName = "taken@example.com",
            Email = "taken@example.com",
            NormalizedEmail = "TAKEN@EXAMPLE.COM",
            NormalizedUserName = "TAKEN@EXAMPLE.COM",
            FirstName = "Old",
            LastName = "Account",
            IsDeleted = true,
        };
        var context = provider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(deleted);
        await context.SaveChangesAsync();

        var service = new AuthService(userManager, new Mock<IJwtProvider>().Object);

        var result = await service.RegisterAsync(
            new RegisterRequest("taken@example.com", "Passw0rd!", "New", "User"));

        Assert.False(result.IsSuccess);
        Assert.Equal(UserErrors.DuplicatedEmail.Code, result.Error.Code);
    }
}
```

The record is `RegisterRequest(string Email, string Password, string FirstName, string LastName)`, so the argument order above is correct. Be aware of a pre-existing oddity when you go looking for it: the two files in `Contracts/Authentication/` have swapped contents — `RegisterRequest` is declared inside `LoginRequest.cs` and vice versa. Leave that alone; it is out of scope here.

- [ ] **Step 2: Run the test to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~AuthServiceRegistrationTests
```

Expected: FAIL — registration currently proceeds past the duplicate check.

- [ ] **Step 3: Check with the filter disabled**

In `backend/Ecommerce/Services/AuthService.cs`, replace the first two lines of `RegisterAsync`:

```csharp
        var emailExists = await _userManager.FindByEmailAsync(request.Email);
        if (emailExists is not null)
            return Result.Failure<AuthResponse>(UserErrors.DuplicatedEmail);
```

with:

```csharp
        // IgnoreQueryFilters: a soft-deleted account still owns its email in the unique index,
        // so it must block re-registration even though it is invisible to every other query.
        var normalizedEmail = _userManager.NormalizeEmail(request.Email);
        var emailExists = await _userManager.Users
            .IgnoreQueryFilters()
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (emailExists)
            return Result.Failure<AuthResponse>(UserErrors.DuplicatedEmail);
```

- [ ] **Step 4: Run the test to verify it passes**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~AuthServiceRegistrationTests
```

Expected: PASS.

- [ ] **Step 5: Run the whole suite**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```bash
git add backend/Ecommerce/Services/AuthService.cs backend/Ecommerce.Tests/Services/AuthServiceRegistrationTests.cs
git commit -m "Block re-registering an email owned by a soft-deleted account"
```

---

## Task 6: `IFileStorage` and `LocalFileStorage`

**Files:**
- Create: `backend/Ecommerce/Errors/FileErrors.cs`
- Create: `backend/Ecommerce/Storage/IFileStorage.cs`
- Create: `backend/Ecommerce/Storage/LocalFileStorage.cs`
- Modify: `backend/Ecommerce/DependacyInjection.cs` (service registration)
- Test: `backend/Ecommerce.Tests/Storage/LocalFileStorageTests.cs`

**Interfaces:**
- Produces: `Ecommerce.Storage.IFileStorage` with
  `Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken cancellationToken = default)`,
  registered `Scoped` as `LocalFileStorage`. On success `Result.Value` is a public relative path of the form `/uploads/{module}/{guid}{ext}`, which callers persist straight into an entity's `Image` string column. On failure it returns `FileErrors.EmptyFile` (`"File.Empty"`), `FileErrors.UnsupportedType` (`"File.UnsupportedType"`), or `FileErrors.TooLarge` (`"File.TooLarge"`). **Plan 2B's Categories and Sliders tasks consume exactly this signature.**

- [ ] **Step 1: Write the failing test**

```csharp
// backend/Ecommerce.Tests/Storage/LocalFileStorageTests.cs
using System.Text;
using Ecommerce.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Ecommerce.Tests.Storage;

public class LocalFileStorageTests
{
    private static (LocalFileStorage Storage, string Root) CreateStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecom-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.WebRootPath).Returns(root);
        environment.SetupGet(x => x.ContentRootPath).Returns(root);

        return (new LocalFileStorage(environment.Object), root);
    }

    private static IFormFile FileNamed(string fileName, int byteCount = 8)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', byteCount)));
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }

    [Fact]
    public async Task SaveAsync_writes_the_file_and_returns_its_public_path()
    {
        var (storage, root) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed("banner.png"), "sliders");

        Assert.True(result.IsSuccess);
        Assert.StartsWith("/uploads/sliders/", result.Value);
        Assert.EndsWith(".png", result.Value);

        var written = Path.Combine(root, result.Value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(written));
    }

    [Fact]
    public async Task SaveAsync_rejects_an_empty_file()
    {
        var (storage, _) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed("banner.png", byteCount: 0), "sliders");

        Assert.False(result.IsSuccess);
        Assert.Equal("File.Empty", result.Error.Code);
    }

    [Fact]
    public async Task SaveAsync_rejects_an_unsupported_extension()
    {
        var (storage, _) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed("payload.svg"), "sliders");

        Assert.False(result.IsSuccess);
        Assert.Equal("File.UnsupportedType", result.Error.Code);
    }

    [Fact]
    public async Task SaveAsync_rejects_a_file_over_two_megabytes()
    {
        var (storage, _) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed("huge.png", byteCount: 2 * 1024 * 1024 + 1), "sliders");

        Assert.False(result.IsSuccess);
        Assert.Equal("File.TooLarge", result.Error.Code);
    }

    [Theory]
    [InlineData("photo.JPG")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.WebP")]
    public async Task SaveAsync_accepts_the_allowed_extensions_case_insensitively(string fileName)
    {
        var (storage, _) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed(fileName), "categories");

        Assert.True(result.IsSuccess);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~LocalFileStorageTests
```

Expected: compile error — `Ecommerce.Storage` does not exist.

- [ ] **Step 3: Add the error catalogue**

```csharp
// backend/Ecommerce/Errors/FileErrors.cs
namespace Ecommerce.Errors;

public class FileErrors
{
    public static readonly Error EmptyFile = new("File.Empty", "No file was uploaded.");
    public static readonly Error UnsupportedType = new("File.UnsupportedType", "Only .jpg, .jpeg, .png and .webp images are allowed.");
    public static readonly Error TooLarge = new("File.TooLarge", "The file exceeds the 2 MB limit.");
}
```

- [ ] **Step 4: Add the interface**

```csharp
// backend/Ecommerce/Storage/IFileStorage.cs
namespace Ecommerce.Storage;

public interface IFileStorage
{
    // Returns the stored file's public relative path, e.g. "/uploads/categories/a1b2....jpg".
    Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Add the local-disk implementation**

```csharp
// backend/Ecommerce/Storage/LocalFileStorage.cs
namespace Ecommerce.Storage;

// Writes under wwwroot/uploads/<module>/ and returns the path the browser will request.
// Replacing an image writes a new file and leaves the old one on disk: a soft-deleted
// record may be restored later, so deleting its image would be unrecoverable.
public class LocalFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxBytes = 2 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment = environment;

    public async Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return Result.Failure<string>(FileErrors.EmptyFile);

        if (file.Length > MaxBytes)
            return Result.Failure<string>(FileErrors.TooLarge);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Result.Failure<string>(FileErrors.UnsupportedType);

        // WebRootPath is null when wwwroot does not exist yet; fall back to where it will be.
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, "uploads", module);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";

        await using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
            await file.CopyToAsync(stream, cancellationToken);

        return Result.Success($"/uploads/{module}/{fileName}");
    }
}
```

If the build complains that `IWebHostEnvironment`, `IFormFile`, or `Result` are unresolved, add the matching `using`s at the top of the file — `GlobalUsings.cs` covers most `Ecommerce.*` namespaces but not necessarily these.

- [ ] **Step 6: Register the service**

In `backend/Ecommerce/DependacyInjection.cs`, add alongside the other `AddScoped` lines (after `services.AddScoped<IAdminService, AdminService>();`):

```csharp
            services.AddScoped<Ecommerce.Storage.IFileStorage, Ecommerce.Storage.LocalFileStorage>();
```

- [ ] **Step 7: Run the test to verify it passes**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~LocalFileStorageTests
```

Expected: PASS, 7 tests.

- [ ] **Step 8: Commit**

```bash
git add backend/Ecommerce/Errors/FileErrors.cs backend/Ecommerce/Storage backend/Ecommerce/DependacyInjection.cs backend/Ecommerce.Tests/Storage/LocalFileStorageTests.cs
git commit -m "Add IFileStorage with a local-disk implementation"
```

---

## Task 7: Serve uploaded files

**Files:**
- Create: `backend/Ecommerce/wwwroot/uploads/.gitkeep`
- Modify: `backend/Ecommerce/Program.cs`
- Modify: `.gitignore` (repo root)

**Interfaces:**
- Consumes: `LocalFileStorage`'s output paths (Task 6).
- Produces: a file saved at `/uploads/<module>/<name>` is fetchable at `https://localhost:7297/uploads/<module>/<name>` without authentication.

- [ ] **Step 1: Create the upload directory**

```powershell
New-Item -ItemType Directory -Force backend/Ecommerce/wwwroot/uploads
New-Item -ItemType File backend/Ecommerce/wwwroot/uploads/.gitkeep
```

- [ ] **Step 2: Keep uploaded content out of version control**

Append to the repo-root `.gitignore`:

```gitignore
# Uploaded media is runtime data, not source. Keep the folder, ignore its contents.
backend/Ecommerce/wwwroot/uploads/*
!backend/Ecommerce/wwwroot/uploads/.gitkeep
```

- [ ] **Step 3: Enable static file serving**

In `backend/Ecommerce/Program.cs`, add `app.UseStaticFiles();` immediately after `app.UseHttpsRedirection();`:

```csharp
app.UseHttpsRedirection();

// Uploaded images under wwwroot/uploads are public by design — served before auth runs.
app.UseStaticFiles();

app.UseCors("AngularAppPolicy");
```

- [ ] **Step 4: Verify by hand**

Start the API:

```powershell
dotnet run --project Ecommerce
```

Then in a second terminal, drop a file in and fetch it:

```powershell
Set-Content -Path backend/Ecommerce/wwwroot/uploads/ping.txt -Value "ok" -Encoding utf8
curl.exe -k https://localhost:7297/uploads/ping.txt
```

Expected: prints `ok`. Then delete the probe file and stop the app:

```powershell
Remove-Item backend/Ecommerce/wwwroot/uploads/ping.txt
```

- [ ] **Step 5: Commit**

```bash
git add .gitignore backend/Ecommerce/Program.cs backend/Ecommerce/wwwroot/uploads/.gitkeep
git commit -m "Serve uploaded files from wwwroot/uploads"
```

---

## Task 8: Close the unauthenticated Products write hole

**Files:**
- Modify: `backend/Ecommerce/Controllers/ProductsController.cs`
- Test: `backend/Ecommerce.Tests/Authorization/ProductsControllerAuthorizationTests.cs`

**Interfaces:**
- Consumes: `PermissionKeys.ProductsManage`, `AdminAuthDefaults.Scheme`, `HasPermissionAttribute` (all pre-existing from Phase 1).
- Produces: `POST`/`PUT`/`DELETE`/`toggleStatus` on `api/Products` require an admin token carrying `products.manage`. The two `GET` actions stay public for the storefront.

`Product` already inherits `AuditableEntity` (Task 1), so stamping and soft-delete come free — this task only adds the attributes. No admin Products UI ships here; that is a later phase.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/Ecommerce.Tests/Authorization/ProductsControllerAuthorizationTests.cs
using System.Reflection;
using Ecommerce.Authorization;
using Ecommerce.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Tests.Authorization;

public class ProductsControllerAuthorizationTests
{
    [Theory]
    [InlineData("Add")]
    [InlineData("Update")]
    [InlineData("Delete")]
    [InlineData("ToggleStatus")]
    public void Write_actions_require_the_products_manage_permission(string actionName)
    {
        var action = typeof(ProductsController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;

        var permission = action.GetCustomAttributes<HasPermissionAttribute>(inherit: true).SingleOrDefault();

        Assert.NotNull(permission);
        Assert.Equal($"{AdminAuthDefaults.PolicyPrefix}{PermissionKeys.ProductsManage}", permission!.Policy);
        Assert.Equal(AdminAuthDefaults.Scheme, permission.AuthenticationSchemes);
    }

    [Theory]
    [InlineData("GetAll")]
    [InlineData("Get")]
    public void Read_actions_stay_public_for_the_storefront(string actionName)
    {
        var action = typeof(ProductsController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;

        Assert.Empty(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
        Assert.Empty(typeof(ProductsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~ProductsControllerAuthorizationTests
```

Expected: the four write-action cases FAIL (`permission` is null); the two read-action cases pass already.

- [ ] **Step 3: Add the attributes**

In `backend/Ecommerce/Controllers/ProductsController.cs`, add this using at the top:

```csharp
using Ecommerce.Authorization;
```

`HasPermissionAttribute` derives from `AuthorizeAttribute`, so `AuthenticationSchemes` is an inherited settable property and can be supplied as a named attribute argument. Its constructor is `params string[] permissions`, and a named argument is legal after a params list.

Add one attribute line directly below the HTTP-verb attribute on **each** of the four write actions:

```csharp
        [HttpPost("")]
        [HasPermission(PermissionKeys.ProductsManage, AuthenticationSchemes = AdminAuthDefaults.Scheme)]
        public async Task<IActionResult> Add([FromForm] ProductRequest request, CancellationToken cancellationToken)
```

```csharp
        [HttpPut("{id}")]
        [HasPermission(PermissionKeys.ProductsManage, AuthenticationSchemes = AdminAuthDefaults.Scheme)]
        public async Task<IActionResult> Update([FromRoute] long id, [FromForm] ProductRequest request, CancellationToken cancellationToken)
```

```csharp
        [HttpDelete("{id}")]
        [HasPermission(PermissionKeys.ProductsManage, AuthenticationSchemes = AdminAuthDefaults.Scheme)]
        public async Task<IActionResult> Delete([FromRoute] long id, CancellationToken cancellationToken)
```

```csharp
        [HttpPut("{id}/toggleStatus")]
        [HasPermission(PermissionKeys.ProductsManage, AuthenticationSchemes = AdminAuthDefaults.Scheme)]
        public async Task<IActionResult> ToggleStatus([FromRoute] long id, CancellationToken cancellationToken)
```

Do not add a class-level `[Authorize]`, and leave the existing `//[Authorize]` comment on the class alone — the two `GET` actions must stay public for the storefront.

- [ ] **Step 4: Run the test to verify it passes**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~ProductsControllerAuthorizationTests
```

Expected: PASS, 6 tests.

- [ ] **Step 5: Verify the hole is really closed, end to end**

Start the API with `dotnet run --project Ecommerce`, then:

```powershell
curl.exe -k -i -X DELETE https://localhost:7297/api/Products/1
```

Expected: `HTTP/1.1 401 Unauthorized` (before this task it would have attempted the delete). Stop the app.

- [ ] **Step 6: Commit**

```bash
git add backend/Ecommerce/Controllers/ProductsController.cs backend/Ecommerce.Tests/Authorization/ProductsControllerAuthorizationTests.cs
git commit -m "Require products.manage on Products write endpoints"
```

---

## Task 9: Confirm the Phase 1 admin paths inherited the new behaviour

**Files:**
- Test: `backend/Ecommerce.Tests/Services/RoleServiceSoftDeleteTests.cs`

**Interfaces:**
- Consumes: `RoleService` (Phase 1), the query filter (Task 3), the `SaveChanges` hook (Task 4).
- Produces: proof that `RoleService.DeleteAsync` soft-deletes and that a deleted role's name becomes reusable — with no change to `RoleService` itself.

`AdminRole` inherits `AuditableEntity`, so `RoleService.DeleteAsync`'s existing `_context.AdminRoles.Remove(role)` is now a soft delete, and its uniqueness check automatically stops counting deleted roles because the filter hides them. This task is verification, not new logic. If a test here fails, the defect is in Task 3 or Task 4 — fix it there, not in `RoleService`.

- [ ] **Step 1: Write the test**

```csharp
// backend/Ecommerce.Tests/Services/RoleServiceSoftDeleteTests.cs
using Ecommerce.Contracts.Roles;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class RoleServiceSoftDeleteTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    [Fact]
    public async Task DeleteAsync_soft_deletes_the_role_instead_of_removing_the_row()
    {
        await using var context = CreateContext();
        var service = new RoleService(context);
        var created = await service.CreateAsync(new RoleRequest("Editor", null, []));

        var result = await service.DeleteAsync(created.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(await context.AdminRoles.ToListAsync());

        var deleted = await context.AdminRoles.IgnoreQueryFilters().SingleAsync(x => x.Id == created.Value.Id);
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedOn);
    }

    [Fact]
    public async Task A_deleted_roles_name_can_be_reused()
    {
        await using var context = CreateContext();
        var service = new RoleService(context);
        var created = await service.CreateAsync(new RoleRequest("Editor", null, []));
        await service.DeleteAsync(created.Value.Id);

        var recreated = await service.CreateAsync(new RoleRequest("Editor", null, []));

        Assert.True(recreated.IsSuccess);
    }

    [Fact]
    public async Task GetAllAsync_does_not_return_deleted_roles()
    {
        await using var context = CreateContext();
        var service = new RoleService(context);
        var keep = await service.CreateAsync(new RoleRequest("Keeper", null, []));
        var drop = await service.CreateAsync(new RoleRequest("Doomed", null, []));
        await service.DeleteAsync(drop.Value.Id);

        var all = await service.GetAllAsync();

        Assert.Single(all.Value);
        Assert.Equal(keep.Value.Id, all.Value.First().Id);
    }
}
```

- [ ] **Step 2: Run the test**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter FullyQualifiedName~RoleServiceSoftDeleteTests
```

Expected: PASS, 3 tests, with no change to `RoleService.cs`. If `DeleteAsync_soft_deletes...` fails, revisit Task 4's `EntityState.Deleted` branch. If `A_deleted_roles_name_can_be_reused` fails, revisit Task 3's filter.

- [ ] **Step 3: Run the whole suite**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: every test passes.

- [ ] **Step 4: Verify the running app end to end**

Start the API (`dotnet run --project Ecommerce`) and the frontend (`npm start` from `frontend/`). Log in at `http://localhost:4200/admin/auth/login` as `admin.tester@example.com` / `AdminTester@123`, then:

1. Go to **Roles**, create a role named `Temp QA`, and delete it. It disappears from the list.
2. Create a role named `Temp QA` again. It succeeds — proving the name was released by the soft delete.
3. Go to **Admins** and create an admin. It appears in the list.
4. Query the database directly and confirm the audit trail was written:

```sql
SELECT TOP 5 Id, Name, IsDeleted, DeletedOn, CreatedById FROM AdminRoles ORDER BY Id DESC;
```

Expected: the first `Temp QA` row is still present with `IsDeleted = 1`, a `DeletedOn` timestamp, and `CreatedById` set to the seeded admin's id.

- [ ] **Step 5: Commit**

```bash
git add backend/Ecommerce.Tests/Services/RoleServiceSoftDeleteTests.cs
git commit -m "Cover soft-delete behaviour on the Phase 1 role paths"
```

---

## Done criteria

- `dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj` passes in full.
- The API boots, the dev seeders run, and `/uploads/<file>` is fetchable.
- `DELETE https://localhost:7297/api/Products/1` returns 401 without an admin token.
- Deleting anything auditable sets `IsDeleted` rather than removing the row, and admin-initiated writes carry a `CreatedById`/`UpdatedById`.
- Plan 2B (`docs/superpowers/plans/2026-08-12-admin-phase2b-categories-clients-sliders.md`) can now start.
