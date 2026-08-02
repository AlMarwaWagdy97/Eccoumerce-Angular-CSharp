# Admin Dashboard Phase 1: Auth, Roles & Admins Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the admin authentication, role/permission, and admin-management foundation that later admin dashboard phases (Categories, Products, Orders, Dashboard/Reports) will authorize against.

**Architecture:** A fully separate `Admin` identity subsystem (own table, own JWT scheme `AdminBearer`, own password hashing via `PasswordHasher<Admin>`) parallel to the existing customer `ApplicationUser`/Identity system. Permissions are DB-backed (`Permission`, `AdminRole`, join table), granted per role, carried as JWT claims, and enforced via a custom `[HasPermission(key)]` policy attribute. Frontend mirrors the existing `site/` auth patterns (signals-based service, guard, interceptor) inside `admin/`, with a new sidebar shell matching the reference design.

**Tech Stack:** ASP.NET Core .NET 10, EF Core (SQL Server), FluentValidation, Mapster, xUnit + Moq + EF Core InMemory (new test project), MailKit (SMTP/Mailtrap). Angular 22 standalone components, signals, Vitest (frontend has no established component/service test convention yet — see Global Constraints).

## Global Constraints

- Follow `backend/CLAUDE.md` conventions: thin controllers → `Scoped` services returning `Result`/`Result<T>`, `ApiResponse<T>` envelope, DTOs as `record`s with FluentValidation validators, Mapster for entity↔DTO mapping is optional per-DTO (use plain mapping methods where Mapster adds no value, matching the `AddressService.MapAddress` precedent), per-domain `*Errors` classes, primary-constructor DI, `.AsNoTracking()` on reads, trailing `CancellationToken` on all service methods.
- Follow `frontend/CLAUDE.md` conventions: Angular 22 bare file naming (no `.component.ts`), standalone components with explicit `imports`, new control-flow syntax (`@if`/`@for`), signals over RxJS subscribe-into-fields for new code, services named `XServices`, external templates/styles.
- No entity, contract, or claim name introduced in one task may be renamed in a later task without updating every consumer — check the **Interfaces** block of each task before writing code that depends on an earlier task.
- Backend has **no existing test project**; frontend has Vitest configured but **no established test convention for services/components** (only the default `app.spec.ts` scaffold exists). Task 1 adds a real backend test project — new backend tasks include real unit tests. Frontend tasks use manual browser verification instead of new Vitest specs, matching the codebase's actual practice; do not add frontend unit tests as part of this plan.
- Permission keys are a single source of truth in `PermissionKeys.cs` (Task 2) — every `[HasPermission(...)]` attribute and every seeded `Permission` row must use one of those constants, never an inline string.
- Admin JWTs and customer JWTs share the same signing `Jwt:Key`/`Jwt:Issuer` but use **different audiences** (`Jwt:Audience` vs `Jwt:AdminAudience`) and different authentication schemes (default `Bearer` vs `AdminBearer`) — a customer token must never satisfy an admin-only endpoint and vice versa.

---

## Task 1: Backend test project

**Files:**
- Create: `backend/Ecommerce.Tests/Ecommerce.Tests.csproj`
- Create: `backend/Ecommerce.Tests/SmokeTests.cs`
- Modify: `backend/Ecommerce.slnx`

**Interfaces:**
- Produces: a `dotnet test` entry point every later backend task's tests run under.

- [ ] **Step 1: Create the test project**

From `backend/`:

```powershell
dotnet new xunit -n Ecommerce.Tests -o Ecommerce.Tests
dotnet add Ecommerce.Tests/Ecommerce.Tests.csproj reference Ecommerce/Ecommerce.csproj
dotnet add Ecommerce.Tests/Ecommerce.Tests.csproj package Moq
dotnet add Ecommerce.Tests/Ecommerce.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory
```

- [ ] **Step 2: Register the project in the solution**

```powershell
dotnet sln Ecommerce.slnx add Ecommerce.Tests/Ecommerce.Tests.csproj
```

- [ ] **Step 3: Write a smoke test**

Replace the generated `Ecommerce.Tests/UnitTest1.cs` (delete it) with:

```csharp
// backend/Ecommerce.Tests/SmokeTests.cs
namespace Ecommerce.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_reference_resolves()
    {
        var error = new Ecommerce.Abstractions.Error("Test.Code", "Test description");
        Assert.Equal("Test.Code", error.Code);
    }
}
```

- [ ] **Step 4: Run the tests**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: 1 passed.

- [ ] **Step 5: Commit**

```bash
git add backend/Ecommerce.slnx backend/Ecommerce.Tests
git commit -m "Add backend test project (xUnit, Moq, EF InMemory)"
```

---

## Task 2: Admin domain entities, EF configuration, and migration

**Files:**
- Create: `backend/Ecommerce/Entities/Admin.cs`
- Create: `backend/Ecommerce/Entities/AdminRole.cs`
- Create: `backend/Ecommerce/Entities/Permission.cs`
- Create: `backend/Ecommerce/Entities/AdminRolePermission.cs`
- Create: `backend/Ecommerce/Entities/AdminRefreshToken.cs`
- Create: `backend/Ecommerce/Entities/AdminPasswordResetToken.cs`
- Create: `backend/Ecommerce/Presistence/EntitiesConfigurations/AdminConfiguration.cs`
- Create: `backend/Ecommerce/Presistence/EntitiesConfigurations/AdminRoleConfiguration.cs`
- Create: `backend/Ecommerce/Presistence/EntitiesConfigurations/PermissionConfiguration.cs`
- Create: `backend/Ecommerce/Authorization/PermissionKeys.cs`
- Modify: `backend/Ecommerce/Presistence/ApplicationDbContext.cs` (add `DbSet`s)
- Test: `backend/Ecommerce.Tests/Entities/AdminModelTests.cs`

**Interfaces:**
- Produces: `Admin { Id, FirstName, LastName, Email, PasswordHash, PhoneNumber?, IsActive, CreatedOn, AdminRoleId, AdminRole, RefreshTokens: List<AdminRefreshToken>, PasswordResetTokens: List<AdminPasswordResetToken> }`; `AdminRole { Id, Name, Description?, IsSystem, Permissions: List<Permission>, Admins: List<Admin> }`; `Permission { Id, Key, Module, Description, AdminRoles: List<AdminRole> }`; `AdminRefreshToken { Token, ExpiresOn, CreatedOn, RevokedOn, IsExpired, IsActive }` (same shape as existing `RefreshToken`); `AdminPasswordResetToken { Token, ExpiresOn, CreatedOn, UsedOn, IsExpired, IsUsable }`.
- Produces: `PermissionKeys` static class with one `const string` per permission and a `Catalog: IReadOnlyList<(string Key, string Module, string Description)>` — every later task's `[HasPermission(...)]` calls and the Task 3 seeder both read from this class.

- [ ] **Step 1: Write the entities**

```csharp
// backend/Ecommerce/Entities/Admin.cs
namespace Ecommerce.Entities;

public class Admin
{
    public long Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public long AdminRoleId { get; set; }
    public AdminRole AdminRole { get; set; } = default!;

    public List<AdminRefreshToken> RefreshTokens { get; set; } = [];
    public List<AdminPasswordResetToken> PasswordResetTokens { get; set; } = [];
}
```

```csharp
// backend/Ecommerce/Entities/AdminRole.cs
namespace Ecommerce.Entities;

public class AdminRole
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }

    public List<Permission> Permissions { get; set; } = [];
    public List<Admin> Admins { get; set; } = [];
}
```

```csharp
// backend/Ecommerce/Entities/Permission.cs
namespace Ecommerce.Entities;

public class Permission
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public List<AdminRole> AdminRoles { get; set; } = [];
}
```

```csharp
// backend/Ecommerce/Entities/AdminRolePermission.cs
namespace Ecommerce.Entities;

public class AdminRolePermission
{
    public long AdminRoleId { get; set; }
    public AdminRole AdminRole { get; set; } = default!;
    public long PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}
```

```csharp
// backend/Ecommerce/Entities/AdminRefreshToken.cs
namespace Ecommerce.Entities;

[Owned]
public class AdminRefreshToken
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresOn { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedOn { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
    public bool IsActive => RevokedOn is null && !IsExpired;
}
```

```csharp
// backend/Ecommerce/Entities/AdminPasswordResetToken.cs
namespace Ecommerce.Entities;

[Owned]
public class AdminPasswordResetToken
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresOn { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? UsedOn { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
    public bool IsUsable => UsedOn is null && !IsExpired;
}
```

- [ ] **Step 2: Write the permission catalog**

```csharp
// backend/Ecommerce/Authorization/PermissionKeys.cs
namespace Ecommerce.Authorization;

public static class PermissionKeys
{
    public const string DashboardView = "dashboard.view";
    public const string CategoriesView = "categories.view";
    public const string CategoriesManage = "categories.manage";
    public const string ClientsView = "clients.view";
    public const string ClientsManage = "clients.manage";
    public const string ProductsView = "products.view";
    public const string ProductsManage = "products.manage";
    public const string OrdersView = "orders.view";
    public const string OrdersManage = "orders.manage";
    public const string SlidersManage = "sliders.manage";
    public const string ReportsView = "reports.view";
    public const string RolesManage = "roles.manage";
    public const string AdminsManage = "admins.manage";

    public static readonly IReadOnlyList<(string Key, string Module, string Description)> Catalog =
    [
        (DashboardView, "Dashboard", "View the dashboard overview"),
        (CategoriesView, "Categories", "View categories"),
        (CategoriesManage, "Categories", "Create, edit, and delete categories"),
        (ClientsView, "Clients", "View customer accounts"),
        (ClientsManage, "Clients", "Edit and toggle customer accounts"),
        (ProductsView, "Products", "View products"),
        (ProductsManage, "Products", "Create, edit, and delete products"),
        (OrdersView, "Orders", "View orders"),
        (OrdersManage, "Orders", "Update order status and details"),
        (SlidersManage, "Sliders", "Manage homepage sliders"),
        (ReportsView, "Reports", "View sales and product reports"),
        (RolesManage, "Roles", "Create, edit, and delete roles and permissions"),
        (AdminsManage, "Admins", "Create, edit, and delete admin users"),
    ];
}
```

- [ ] **Step 3: Write the EF configurations**

```csharp
// backend/Ecommerce/Presistence/EntitiesConfigurations/AdminConfiguration.cs
namespace Ecommerce.Presistence.EntitiesConfigurations;

public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);

        builder.HasOne(x => x.AdminRole)
               .WithMany(x => x.Admins)
               .HasForeignKey(x => x.AdminRoleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(x => x.RefreshTokens)
               .ToTable("AdminRefreshTokens")
               .WithOwner()
               .HasForeignKey("AdminId");

        builder.OwnsMany(x => x.PasswordResetTokens)
               .ToTable("AdminPasswordResetTokens")
               .WithOwner()
               .HasForeignKey("AdminId");
    }
}
```

```csharp
// backend/Ecommerce/Presistence/EntitiesConfigurations/AdminRoleConfiguration.cs
namespace Ecommerce.Presistence.EntitiesConfigurations;

public class AdminRoleConfiguration : IEntityTypeConfiguration<AdminRole>
{
    public void Configure(EntityTypeBuilder<AdminRole> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasMany(x => x.Permissions)
               .WithMany(x => x.AdminRoles)
               .UsingEntity<AdminRolePermission>(
                   j => j.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId),
                   j => j.HasOne(x => x.AdminRole).WithMany().HasForeignKey(x => x.AdminRoleId),
                   j =>
                   {
                       j.ToTable("AdminRolePermissions");
                       j.HasKey(x => new { x.AdminRoleId, x.PermissionId });
                   });
    }
}
```

```csharp
// backend/Ecommerce/Presistence/EntitiesConfigurations/PermissionConfiguration.cs
namespace Ecommerce.Presistence.EntitiesConfigurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
    }
}
```

- [ ] **Step 4: Wire up the `DbSet`s**

In `backend/Ecommerce/Presistence/ApplicationDbContext.cs`, add alongside the existing `DbSet<Card> Cards`:

```csharp
        public DbSet<Admin> Admins { get; set; }
        public DbSet<AdminRole> AdminRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
```

(No `using Ecommerce.Authorization;` needed here — the context doesn't reference `PermissionKeys`.)

- [ ] **Step 5: Write a model test (no DB required — EF InMemory)**

```csharp
// backend/Ecommerce.Tests/Entities/AdminModelTests.cs
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Entities;

public class AdminModelTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new NoopHttpContextAccessor());
    }

    [Fact]
    public async Task Admin_role_and_permissions_round_trip()
    {
        await using var context = CreateContext();

        var permission = new Permission { Key = "products.manage", Module = "Products", Description = "Manage products" };
        var role = new AdminRole { Name = "Manager", Permissions = [permission] };
        var admin = new Admin
        {
            FirstName = "Test",
            LastName = "Admin",
            Email = "test.admin@example.com",
            PasswordHash = "hash",
            AdminRole = role,
        };

        context.Admins.Add(admin);
        await context.SaveChangesAsync();

        var loaded = await context.Admins
            .Include(x => x.AdminRole).ThenInclude(x => x.Permissions)
            .FirstAsync(x => x.Email == "test.admin@example.com");

        Assert.Equal("Manager", loaded.AdminRole.Name);
        Assert.Single(loaded.AdminRole.Permissions);
        Assert.Equal("products.manage", loaded.AdminRole.Permissions[0].Key);
    }
}
```

This test needs a fake `IHttpContextAccessor` (the context's constructor requires one). Add it once, reused by later tests:

```csharp
// backend/Ecommerce.Tests/NoopHttpContextAccessor.cs
using Microsoft.AspNetCore.Http;

namespace Ecommerce.Tests;

public class NoopHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }
}
```

- [ ] **Step 6: Run the test to verify it passes**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminModelTests
```

Expected: 1 passed.

- [ ] **Step 7: Add and apply the migration**

From `backend/`:

```powershell
dotnet ef migrations add AddAdminAuth --project Ecommerce
dotnet ef database update --project Ecommerce
```

Verify the migration file creates `Admins`, `AdminRoles`, `Permissions`, `AdminRolePermissions`, `AdminRefreshTokens`, `AdminPasswordResetTokens` tables. If `dotnet ef` isn't on PATH, run `dotnet tool install --global dotnet-ef` first.

- [ ] **Step 8: Commit**

```bash
git add backend/Ecommerce/Entities/Admin.cs backend/Ecommerce/Entities/AdminRole.cs backend/Ecommerce/Entities/Permission.cs backend/Ecommerce/Entities/AdminRolePermission.cs backend/Ecommerce/Entities/AdminRefreshToken.cs backend/Ecommerce/Entities/AdminPasswordResetToken.cs backend/Ecommerce/Authorization/PermissionKeys.cs backend/Ecommerce/Presistence/EntitiesConfigurations/AdminConfiguration.cs backend/Ecommerce/Presistence/EntitiesConfigurations/AdminRoleConfiguration.cs backend/Ecommerce/Presistence/EntitiesConfigurations/PermissionConfiguration.cs backend/Ecommerce/Presistence/ApplicationDbContext.cs backend/Ecommerce/Migrations backend/Ecommerce.Tests
git commit -m "Add Admin/AdminRole/Permission entities, EF config, and migration"
```

---

## Task 3: Dev seeder — permission catalog, Super Admin role, seeded admin

**Files:**
- Create: `backend/Ecommerce/Presistence/AdminDataSeeder.cs`
- Modify: `backend/Ecommerce/Program.cs`

**Interfaces:**
- Consumes: `PermissionKeys.Catalog` (Task 2), `Admin`/`AdminRole`/`Permission` entities (Task 2).
- Produces: a seeded admin at `SeedAdminEmail = "admin.tester@example.com"` / `SeedAdminPassword = "AdminTester@123"` with role `"Super Admin"` holding every permission — later tasks' manual verification steps (Task 8 onward) log in as this admin. Uses `PasswordHasher<Admin>` directly (Task 4/6 introduce this same hasher in `AdminAuthService` — keep the algorithm identical: `new PasswordHasher<Admin>().HashPassword(admin, password)`).

- [ ] **Step 1: Write the seeder**

```csharp
// backend/Ecommerce/Presistence/AdminDataSeeder.cs
using Ecommerce.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Presistence;

// Development-only seed data: the full permission catalog, a built-in
// "Super Admin" role with every permission, and one seeded admin account —
// mirrors the pattern in DataSeeder.cs for the customer side.
public static class AdminDataSeeder
{
    public const string SeedAdminEmail = "admin.tester@example.com";
    public const string SeedAdminPassword = "AdminTester@123";
    public const string SuperAdminRoleName = "Super Admin";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var (key, module, description) in PermissionKeys.Catalog)
        {
            if (!await context.Permissions.AnyAsync(x => x.Key == key))
                context.Permissions.Add(new Permission { Key = key, Module = module, Description = description });
        }
        await context.SaveChangesAsync();

        var superAdminRole = await context.AdminRoles
            .Include(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Name == SuperAdminRoleName);

        if (superAdminRole is null)
        {
            superAdminRole = new AdminRole { Name = SuperAdminRoleName, Description = "Full system access", IsSystem = true };
            context.AdminRoles.Add(superAdminRole);
        }

        var allPermissions = await context.Permissions.ToListAsync();
        var missingPermissions = allPermissions.Where(p => !superAdminRole.Permissions.Any(rp => rp.Id == p.Id));
        superAdminRole.Permissions.AddRange(missingPermissions);
        await context.SaveChangesAsync();

        if (!await context.Admins.AnyAsync(x => x.Email == SeedAdminEmail))
        {
            var admin = new Admin
            {
                FirstName = "Admin",
                LastName = "Tester",
                Email = SeedAdminEmail,
                AdminRoleId = superAdminRole.Id,
                IsActive = true,
            };
            admin.PasswordHash = new PasswordHasher<Admin>().HashPassword(admin, SeedAdminPassword);

            context.Admins.Add(admin);
            await context.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 2: Call it from `Program.cs`**

In `backend/Ecommerce/Program.cs`, inside the existing `if (app.Environment.IsDevelopment())` block, right after the existing `await Ecommerce.Presistence.DataSeeder.SeedAsync(app.Services);` line:

```csharp
    await Ecommerce.Presistence.AdminDataSeeder.SeedAsync(app.Services);
```

- [ ] **Step 3: Run and verify**

```powershell
dotnet run --project backend/Ecommerce
```

Watch the console for EF Core `INSERT` statements against `Permissions`, `AdminRoles`, `AdminRolePermissions`, and `Admins` on first run (no errors). Stop the app, run it again — no new inserts should happen (idempotent).

- [ ] **Step 4: Commit**

```bash
git add backend/Ecommerce/Presistence/AdminDataSeeder.cs backend/Ecommerce/Program.cs
git commit -m "Add dev seeder for permission catalog, Super Admin role, and seeded admin"
```

---

## Task 4: AdminJwtProvider (permission-carrying tokens)

**Files:**
- Create: `backend/Ecommerce/Authentication/IAdminJwtProvider.cs`
- Create: `backend/Ecommerce/Authentication/AdminJwtProvider.cs`
- Create: `backend/Ecommerce/Authorization/AdminAuthDefaults.cs`
- Modify: `backend/Ecommerce/Authentication/JwtOptions.cs` (add `AdminAudience`)
- Modify: `backend/Ecommerce/appsettings.json` (add `Jwt:AdminAudience`)
- Test: `backend/Ecommerce.Tests/Authentication/AdminJwtProviderTests.cs`

**Interfaces:**
- Consumes: `Admin`/`AdminRole` (Task 2).
- Produces: `IAdminJwtProvider.GenerateToken(Admin admin, IEnumerable<string> permissions) -> (string token, int expiresIn)` and `IAdminJwtProvider.ValidateToken(string token) -> string?` (returns the admin id as a string, or null) — Task 6's `AdminAuthService` calls both. Produces `AdminAuthDefaults.Scheme = "AdminBearer"` and `AdminAuthDefaults.PolicyPrefix = "Permission:"` — Task 5 and every `[HasPermission]`-protected controller depend on these exact constants.

- [ ] **Step 1: Add `AdminAudience` to `JwtOptions`**

```csharp
// backend/Ecommerce/Authentication/JwtOptions.cs — add after Audience
    [Required]
    public string AdminAudience { get; init; } = string.Empty;
```

- [ ] **Step 2: Add the setting to `appsettings.json`**

In `backend/Ecommerce/appsettings.json`, inside the `"Jwt"` section, add:

```json
    "AdminAudience": "EcommerceApp admin users",
```

- [ ] **Step 3: Define the scheme/policy-prefix constants**

```csharp
// backend/Ecommerce/Authorization/AdminAuthDefaults.cs
namespace Ecommerce.Authorization;

public static class AdminAuthDefaults
{
    public const string Scheme = "AdminBearer";
    public const string PolicyPrefix = "Permission:";
}
```

- [ ] **Step 2: Write the failing test**

```csharp
// backend/Ecommerce.Tests/Authentication/AdminJwtProviderTests.cs
using Ecommerce.Authentication;
using Ecommerce.Entities;
using Microsoft.Extensions.Options;

namespace Ecommerce.Tests.Authentication;

public class AdminJwtProviderTests
{
    private static AdminJwtProvider CreateProvider() => new(Options.Create(new JwtOptions
    {
        Key = "ThisIsAVeryLongAndSecureSecretKeyThatIsAtLeast32CharactersLong",
        Issuer = "EcommerceApp",
        Audience = "EcommerceApp users",
        AdminAudience = "EcommerceApp admin users",
        ExpiryMinutes = 30,
    }));

    private static Admin CreateAdmin() => new()
    {
        Id = 7,
        Email = "test.admin@example.com",
        FirstName = "Test",
        LastName = "Admin",
        AdminRole = new AdminRole { Id = 1, Name = "Manager" },
    };

    [Fact]
    public void GenerateToken_includes_role_and_permission_claims()
    {
        var provider = CreateProvider();
        var admin = CreateAdmin();

        var (token, expiresIn) = provider.GenerateToken(admin, ["products.manage", "orders.view"]);

        Assert.NotEmpty(token);
        Assert.Equal(1800, expiresIn);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("Manager", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.Equal(2, jwt.Claims.Count(c => c.Type == "permission"));
        Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "products.manage");
        Assert.Equal("EcommerceApp admin users", jwt.Audiences.Single());
    }

    [Fact]
    public void ValidateToken_returns_admin_id_for_a_token_it_issued()
    {
        var provider = CreateProvider();
        var (token, _) = provider.GenerateToken(CreateAdmin(), []);

        var adminId = provider.ValidateToken(token);

        Assert.Equal("7", adminId);
    }

    [Fact]
    public void ValidateToken_returns_null_for_garbage_input()
    {
        var provider = CreateProvider();

        Assert.Null(provider.ValidateToken("not-a-real-token"));
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminJwtProviderTests
```

Expected: FAIL — `IAdminJwtProvider`/`AdminJwtProvider` don't exist yet.

- [ ] **Step 4: Write the interface and implementation**

```csharp
// backend/Ecommerce/Authentication/IAdminJwtProvider.cs
namespace Ecommerce.Authentication;

public interface IAdminJwtProvider
{
    (string token, int expiresIn) GenerateToken(Admin admin, IEnumerable<string> permissions);
    string? ValidateToken(string token);
}
```

```csharp
// backend/Ecommerce/Authentication/AdminJwtProvider.cs
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ecommerce.Authentication;

public class AdminJwtProvider(IOptions<JwtOptions> options) : IAdminJwtProvider
{
    private readonly JwtOptions _options = options.Value;

    public (string token, int expiresIn) GenerateToken(Admin admin, IEnumerable<string> permissions)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, admin.Email),
            new(JwtRegisteredClaimNames.GivenName, admin.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, admin.LastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", admin.AdminRole.Name),
        ];
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));

        var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.AdminAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: signingCredentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), _options.ExpiryMinutes * 60);
    }

    public string? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                IssuerSigningKey = symmetricSecurityKey,
                ValidateIssuerSigningKey = true,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            return jwtToken.Claims.First(x => x.Type == JwtRegisteredClaimNames.Sub).Value;
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminJwtProviderTests
```

Expected: 3 passed.

- [ ] **Step 6: Commit**

```bash
git add backend/Ecommerce/Authentication/IAdminJwtProvider.cs backend/Ecommerce/Authentication/AdminJwtProvider.cs backend/Ecommerce/Authentication/JwtOptions.cs backend/Ecommerce/Authorization/AdminAuthDefaults.cs backend/Ecommerce/appsettings.json backend/Ecommerce.Tests/Authentication
git commit -m "Add AdminJwtProvider with role/permission claims and a dedicated admin audience"
```

---

## Task 5: Permission-based authorization (`[HasPermission]`) + `AdminBearer` scheme

**Files:**
- Create: `backend/Ecommerce/Authorization/PermissionRequirement.cs`
- Create: `backend/Ecommerce/Authorization/PermissionAuthorizationHandler.cs`
- Create: `backend/Ecommerce/Authorization/PermissionPolicyProvider.cs`
- Create: `backend/Ecommerce/Authorization/HasPermissionAttribute.cs`
- Modify: `backend/Ecommerce/DependacyInjection.cs` (register `AdminBearer` scheme + policy provider/handler)
- Test: `backend/Ecommerce.Tests/Authorization/PermissionAuthorizationHandlerTests.cs`

**Interfaces:**
- Consumes: `AdminAuthDefaults.Scheme`, `AdminAuthDefaults.PolicyPrefix` (Task 4).
- Produces: `[HasPermission(PermissionKeys.AdminsManage)]` — every admin controller action from Task 8 onward is decorated with this instead of `[Authorize]`. A request's `ClaimsPrincipal` must carry a `"permission"` claim equal to the required key (as issued by `AdminJwtProvider` in Task 4) to pass.

- [ ] **Step 1: Write the failing test**

```csharp
// backend/Ecommerce.Tests/Authorization/PermissionAuthorizationHandlerTests.cs
using Ecommerce.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Ecommerce.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext CreateContext(string requiredPermission, params string[] grantedPermissions)
    {
        var claims = grantedPermissions.Select(p => new Claim("permission", p));
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var requirement = new PermissionRequirement(requiredPermission);

        return new AuthorizationHandlerContext([requirement], principal, null);
    }

    [Fact]
    public async Task Succeeds_when_the_user_has_the_required_permission_claim()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = CreateContext("products.manage", "products.manage", "orders.view");

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_when_the_user_is_missing_the_required_permission_claim()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = CreateContext("admins.manage", "products.manage");

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter PermissionAuthorizationHandlerTests
```

Expected: FAIL — `PermissionRequirement`/`PermissionAuthorizationHandler` don't exist yet.

- [ ] **Step 3: Write the requirement and handler**

```csharp
// backend/Ecommerce/Authorization/PermissionRequirement.cs
namespace Ecommerce.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
```

```csharp
// backend/Ecommerce/Authorization/PermissionAuthorizationHandler.cs
namespace Ecommerce.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("permission", requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter PermissionAuthorizationHandlerTests
```

Expected: 2 passed.

- [ ] **Step 5: Write the dynamic policy provider and attribute (no test — thin ASP.NET Core wiring, verified via Task 8's manual e2e check)**

```csharp
// backend/Ecommerce/Authorization/PermissionPolicyProvider.cs
namespace Ecommerce.Authorization;

public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(AdminAuthDefaults.PolicyPrefix, StringComparison.Ordinal))
            return _fallback.GetPolicyAsync(policyName);

        var permission = policyName[AdminAuthDefaults.PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder(AdminAuthDefaults.Scheme)
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
```

```csharp
// backend/Ecommerce/Authorization/HasPermissionAttribute.cs
namespace Ecommerce.Authorization;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = $"{AdminAuthDefaults.PolicyPrefix}{permission}";
    }
}
```

- [ ] **Step 6: Register the `AdminBearer` scheme and the policy provider/handler**

In `backend/Ecommerce/DependacyInjection.cs`, inside `AddAuthConfig`, after the existing `.AddJwtBearer(o => { ... })` call for the default scheme, chain a second `.AddJwtBearer(...)` for `AdminBearer` (same method chain, don't call `AddAuthentication` twice):

```csharp
            .AddJwtBearer(o =>
            {
                o.SaveToken = true;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key!)),
                    ValidIssuer = jwtSettings?.Issuer,
                    ValidAudience = jwtSettings?.Audience
                };
            })
            .AddJwtBearer(Ecommerce.Authorization.AdminAuthDefaults.Scheme, o =>
            {
                o.SaveToken = true;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings?.Key!)),
                    ValidIssuer = jwtSettings?.Issuer,
                    ValidAudience = jwtSettings?.AdminAudience
                };
            });

            services.AddSingleton<IAuthorizationPolicyProvider, Ecommerce.Authorization.PermissionPolicyProvider>();
            services.AddSingleton<IAuthorizationHandler, Ecommerce.Authorization.PermissionAuthorizationHandler>();
            services.AddSingleton<IAdminJwtProvider, AdminJwtProvider>();
```

(`services.AddAuthorization();` is not called explicitly anywhere in this file today, and MVC's `AddControllers()` already registers the authorization services it needs — the two lines above only add to that container, they don't need a preceding `AddAuthorization()` call.)

- [ ] **Step 7: Build to verify it compiles**

```powershell
dotnet build backend/Ecommerce.slnx
```

Expected: 0 errors.

- [ ] **Step 8: Commit**

```bash
git add backend/Ecommerce/Authorization backend/Ecommerce/DependacyInjection.cs backend/Ecommerce.Tests/Authorization
git commit -m "Add permission-based authorization (HasPermissionAttribute) and the AdminBearer scheme"
```

---

## Task 6: Email sending (Mailtrap SMTP)

**Files:**
- Create: `backend/Ecommerce/Email/IEmailSender.cs`
- Create: `backend/Ecommerce/Email/SmtpEmailSender.cs`
- Create: `backend/Ecommerce/Email/SmtpOptions.cs`
- Modify: `backend/Ecommerce/appsettings.json` (add empty `Smtp` section — real credentials go in user secrets)
- Modify: `backend/Ecommerce/DependacyInjection.cs` (bind `SmtpOptions`, register `IEmailSender`)
- Test: `backend/Ecommerce.Tests/Email/SmtpEmailSenderTests.cs`

**Interfaces:**
- Produces: `IEmailSender.SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default) -> Task` — Task 7's `AdminAuthService.ForgotPasswordAsync` and Task 11's admin-creation flow both call this.

- [ ] **Step 1: Add the MailKit package**

```powershell
dotnet add backend/Ecommerce/Ecommerce.csproj package MailKit
```

- [ ] **Step 2: Write `SmtpOptions`**

```csharp
// backend/Ecommerce/Email/SmtpOptions.cs
using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Email;

public class SmtpOptions
{
    public static string SectionName = "Smtp";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; }

    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    [Required]
    public string FromEmail { get; init; } = string.Empty;

    public string FromName { get; init; } = "ShopDemo Admin";
}
```

- [ ] **Step 3: Write the failing test for message construction**

`SmtpEmailSender` splits message-building (pure, testable) from sending (network I/O, not unit-tested — verified manually in Step 7):

```csharp
// backend/Ecommerce.Tests/Email/SmtpEmailSenderTests.cs
using Ecommerce.Email;
using Microsoft.Extensions.Options;

namespace Ecommerce.Tests.Email;

public class SmtpEmailSenderTests
{
    private static SmtpEmailSender CreateSender() => new(Options.Create(new SmtpOptions
    {
        Host = "sandbox.smtp.mailtrap.io",
        Port = 2525,
        Username = "test-user",
        Password = "test-pass",
        FromEmail = "no-reply@shopdemo.local",
        FromName = "ShopDemo Admin",
    }));

    [Fact]
    public void BuildMessage_sets_from_to_subject_and_html_body()
    {
        var sender = CreateSender();

        var message = sender.BuildMessage("someone@example.com", "Reset your password", "<p>Click here</p>");

        Assert.Equal("no-reply@shopdemo.local", message.From.Mailboxes.Single().Address);
        Assert.Equal("someone@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("Reset your password", message.Subject);
        Assert.Contains("Click here", message.HtmlBody);
    }
}
```

- [ ] **Step 4: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter SmtpEmailSenderTests
```

Expected: FAIL — `SmtpEmailSender` doesn't exist yet.

- [ ] **Step 5: Write `IEmailSender` and `SmtpEmailSender`**

```csharp
// backend/Ecommerce/Email/IEmailSender.cs
namespace Ecommerce.Email;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
```

```csharp
// backend/Ecommerce/Email/SmtpEmailSender.cs
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Ecommerce.Email;

public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public MimeMessage BuildMessage(string toEmail, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        return message;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var message = BuildMessage(toEmail, subject, htmlBody);

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter SmtpEmailSenderTests
```

Expected: 1 passed.

- [ ] **Step 7: Add config and DI registration**

In `backend/Ecommerce/appsettings.json`, add an empty section (values come from user secrets, same pattern as `Jwt:Key`):

```json
  "Smtp": {
    "Host": "",
    "Port": 2525,
    "Username": "",
    "Password": "",
    "FromEmail": "no-reply@shopdemo.local",
    "FromName": "ShopDemo Admin"
  },
```

In `backend/Ecommerce/DependacyInjection.cs`, inside `AddDependancies`, alongside the existing `services.AddOptions<JwtOptions>()...` block:

```csharp
            services.AddOptions<SmtpOptions>()
                .BindConfiguration(SmtpOptions.SectionName)
                .ValidateOnStart();

            services.AddScoped<IEmailSender, SmtpEmailSender>();
```

Add `using Ecommerce.Email;` to the top of `DependacyInjection.cs`.

- [ ] **Step 8: Supply real Mailtrap credentials via user secrets**

Sign up for a free Mailtrap sandbox inbox (if not already done), then from `backend/`:

```powershell
dotnet user-secrets set "Smtp:Host" "sandbox.smtp.mailtrap.io" --project Ecommerce
dotnet user-secrets set "Smtp:Port" "2525" --project Ecommerce
dotnet user-secrets set "Smtp:Username" "<your-mailtrap-username>" --project Ecommerce
dotnet user-secrets set "Smtp:Password" "<your-mailtrap-password>" --project Ecommerce
```

- [ ] **Step 9: Manually verify a real send**

This is exercised end-to-end once `AdminAuthService.ForgotPasswordAsync` exists (Task 7/9) — no standalone manual check here; note it as pending and confirm together with Task 9's manual verification.

- [ ] **Step 10: Commit**

```bash
git add backend/Ecommerce/Email backend/Ecommerce/appsettings.json backend/Ecommerce/DependacyInjection.cs backend/Ecommerce/Ecommerce.csproj backend/Ecommerce.Tests/Email
git commit -m "Add IEmailSender/SmtpEmailSender (Mailtrap) for admin password reset emails"
```

---

## Task 7: AdminAuthService — login, refresh, logout

**Files:**
- Create: `backend/Ecommerce/Contracts/AdminAuth/AdminLoginRequest.cs`
- Create: `backend/Ecommerce/Contracts/AdminAuth/AdminRefreshTokenRequest.cs`
- Create: `backend/Ecommerce/Contracts/AdminAuth/AdminAuthResponse.cs`
- Create: `backend/Ecommerce/Errors/AdminAuthErrors.cs`
- Create: `backend/Ecommerce/Services/IAdminAuthService.cs`
- Create: `backend/Ecommerce/Services/AdminAuthService.cs`
- Test: `backend/Ecommerce.Tests/Services/AdminAuthServiceTests.cs`

**Interfaces:**
- Consumes: `IAdminJwtProvider` (Task 4), `Admin`/`AdminRole`/`Permission`/`AdminRefreshToken` (Task 2).
- Produces: `IAdminAuthService.LoginAsync(string email, string password, CancellationToken) -> Result<AdminAuthResponse>`, `.RefreshTokenAsync(string token, string refreshToken, CancellationToken) -> Result<AdminAuthResponse>`, `.LogoutAsync(long adminId, CancellationToken) -> Result` — Task 8's `AdminAuthController` calls all three. `AdminAuthResponse { Id: long, Email, FirstName, LastName, RoleName, Permissions: string[], Token, ExpiresIn, RefreshToken, RefreshTokenExpiration }` — the frontend `AdminAuthInterfaces.AdminAuthResponseInterface` (Task 12) must mirror this field-for-field (camelCase).

- [ ] **Step 1: Write the contracts and errors**

```csharp
// backend/Ecommerce/Contracts/AdminAuth/AdminLoginRequest.cs
namespace Ecommerce.Contracts.AdminAuth;

public record AdminLoginRequest(string Email, string Password);

public class AdminLoginRequestValidator : AbstractValidator<AdminLoginRequest>
{
    public AdminLoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
```

```csharp
// backend/Ecommerce/Contracts/AdminAuth/AdminRefreshTokenRequest.cs
namespace Ecommerce.Contracts.AdminAuth;

public record AdminRefreshTokenRequest(string Token, string RefreshToken);

public class AdminRefreshTokenRequestValidator : AbstractValidator<AdminRefreshTokenRequest>
{
    public AdminRefreshTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
```

```csharp
// backend/Ecommerce/Contracts/AdminAuth/AdminAuthResponse.cs
namespace Ecommerce.Contracts.AdminAuth;

public record AdminAuthResponse(
    long Id,
    string Email,
    string FirstName,
    string LastName,
    string RoleName,
    string[] Permissions,
    string Token,
    int ExpiresIn,
    string RefreshToken,
    DateTime RefreshTokenExpiration
);
```

```csharp
// backend/Ecommerce/Errors/AdminAuthErrors.cs
namespace Ecommerce.Errors;

public static class AdminAuthErrors
{
    public static readonly Error InvalidCredentials = new("AdminAuth.InvalidCredentials", "Invalid email or password");
    public static readonly Error AccountInactive = new("AdminAuth.AccountInactive", "This admin account has been deactivated");
    public static readonly Error InvalidJwtToken = new("AdminAuth.InvalidJwtToken", "Invalid JWT token");
    public static readonly Error InvalidRefreshToken = new("AdminAuth.InvalidRefreshToken", "Invalid refresh token");
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// backend/Ecommerce.Tests/Services/AdminAuthServiceTests.cs
using Ecommerce.Authentication;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ecommerce.Tests.Services;

public class AdminAuthServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static Mock<IAdminJwtProvider> CreateJwtProviderMock() =>
        new Mock<IAdminJwtProvider>()
            .Also(m => m.Setup(x => x.GenerateToken(It.IsAny<Admin>(), It.IsAny<IEnumerable<string>>()))
                        .Returns(("fake-jwt", 1800)));

    private static async Task<Admin> SeedAdminAsync(ApplicationDbContext context, string password, bool isActive = true)
    {
        var role = new AdminRole { Name = "Manager", Permissions = [new Permission { Key = "products.manage", Module = "Products", Description = "x" }] };
        var admin = new Admin { FirstName = "Test", LastName = "Admin", Email = "test.admin@example.com", AdminRole = role, IsActive = isActive };
        admin.PasswordHash = new PasswordHasher<Admin>().HashPassword(admin, password);

        context.Admins.Add(admin);
        await context.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task LoginAsync_returns_success_with_permissions_for_correct_credentials()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123");
        var jwtProvider = CreateJwtProviderMock();
        var service = new AdminAuthService(context, jwtProvider.Object);

        var result = await service.LoginAsync("test.admin@example.com", "Correct#123");

        Assert.True(result.IsSuccess);
        Assert.Equal("Manager", result.Value.RoleName);
        Assert.Contains("products.manage", result.Value.Permissions);
    }

    [Fact]
    public async Task LoginAsync_fails_for_wrong_password()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123");
        var service = new AdminAuthService(context, CreateJwtProviderMock().Object);

        var result = await service.LoginAsync("test.admin@example.com", "Wrong#123");

        Assert.False(result.IsSuccess);
        Assert.Equal("AdminAuth.InvalidCredentials", result.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_fails_for_a_deactivated_admin()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123", isActive: false);
        var service = new AdminAuthService(context, CreateJwtProviderMock().Object);

        var result = await service.LoginAsync("test.admin@example.com", "Correct#123");

        Assert.False(result.IsSuccess);
        Assert.Equal("AdminAuth.AccountInactive", result.Error.Code);
    }

    [Fact]
    public async Task LogoutAsync_revokes_all_active_refresh_tokens()
    {
        await using var context = CreateContext();
        var admin = await SeedAdminAsync(context, "Correct#123");
        admin.RefreshTokens.Add(new AdminRefreshToken { Token = "rt-1", ExpiresOn = DateTime.UtcNow.AddDays(14) });
        await context.SaveChangesAsync();
        var service = new AdminAuthService(context, CreateJwtProviderMock().Object);

        var result = await service.LogoutAsync(admin.Id);

        Assert.True(result.IsSuccess);
        var reloaded = await context.Admins.FirstAsync(x => x.Id == admin.Id);
        Assert.All(reloaded.RefreshTokens, rt => Assert.False(rt.IsActive));
    }
}

internal static class MockExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminAuthServiceTests
```

Expected: FAIL — `IAdminAuthService`/`AdminAuthService` don't exist yet.

- [ ] **Step 4: Write `IAdminAuthService` and `AdminAuthService`**

```csharp
// backend/Ecommerce/Services/IAdminAuthService.cs
using Ecommerce.Contracts.AdminAuth;

namespace Ecommerce.Services;

public interface IAdminAuthService
{
    Task<Result<AdminAuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AdminAuthResponse>> RefreshTokenAsync(string token, string refreshToken, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(long adminId, CancellationToken cancellationToken = default);
}
```

```csharp
// backend/Ecommerce/Services/AdminAuthService.cs
using Ecommerce.Authentication;
using Ecommerce.Contracts.AdminAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Services;

public class AdminAuthService(ApplicationDbContext context, IAdminJwtProvider jwtProvider) : IAdminAuthService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IAdminJwtProvider _jwtProvider = jwtProvider;
    private readonly PasswordHasher<Admin> _passwordHasher = new();
    private readonly int _refreshTokenExpiryDays = 14;

    public async Task<Result<AdminAuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins
            .Include(x => x.AdminRole).ThenInclude(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (admin is null || _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Failed)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.InvalidCredentials);

        if (!admin.IsActive)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.AccountInactive);

        return Result.Success(await IssueTokensAsync(admin, cancellationToken));
    }

    public async Task<Result<AdminAuthResponse>> RefreshTokenAsync(string token, string refreshToken, CancellationToken cancellationToken = default)
    {
        var adminId = _jwtProvider.ValidateToken(token);
        if (adminId is null || !long.TryParse(adminId, out var id))
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.InvalidJwtToken);

        var admin = await _context.Admins
            .Include(x => x.AdminRole).ThenInclude(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (admin is null)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.InvalidJwtToken);

        var activeToken = admin.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken && x.IsActive);
        if (activeToken is null)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.InvalidRefreshToken);

        if (!admin.IsActive)
            return Result.Failure<AdminAuthResponse>(AdminAuthErrors.AccountInactive);

        activeToken.RevokedOn = DateTime.UtcNow;

        return Result.Success(await IssueTokensAsync(admin, cancellationToken));
    }

    public async Task<Result> LogoutAsync(long adminId, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Id == adminId, cancellationToken);
        if (admin is null)
            return Result.Failure(AdminAuthErrors.InvalidJwtToken);

        foreach (var refreshToken in admin.RefreshTokens.Where(x => x.IsActive))
            refreshToken.RevokedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<AdminAuthResponse> IssueTokensAsync(Admin admin, CancellationToken cancellationToken)
    {
        var permissions = admin.AdminRole.Permissions.Select(p => p.Key).ToArray();
        var (jwt, expiresIn) = _jwtProvider.GenerateToken(admin, permissions);

        var refreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        var refreshTokenExpiration = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays);

        admin.RefreshTokens.Add(new AdminRefreshToken { Token = refreshToken, ExpiresOn = refreshTokenExpiration });
        await _context.SaveChangesAsync(cancellationToken);

        return new AdminAuthResponse(
            admin.Id, admin.Email, admin.FirstName, admin.LastName, admin.AdminRole.Name, permissions,
            jwt, expiresIn, refreshToken, refreshTokenExpiration);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminAuthServiceTests
```

Expected: 4 passed.

- [ ] **Step 6: Register the service**

In `backend/Ecommerce/DependacyInjection.cs`, alongside the existing `services.AddScoped<IAuthService, AuthService>();`:

```csharp
            services.AddScoped<IAdminAuthService, AdminAuthService>();
```

- [ ] **Step 7: Commit**

```bash
git add backend/Ecommerce/Contracts/AdminAuth backend/Ecommerce/Errors/AdminAuthErrors.cs backend/Ecommerce/Services/IAdminAuthService.cs backend/Ecommerce/Services/AdminAuthService.cs backend/Ecommerce/DependacyInjection.cs backend/Ecommerce.Tests/Services
git commit -m "Add AdminAuthService (login, refresh, logout)"
```

---

## Task 8: AdminAuthController (login, refresh, logout) — manual e2e verification

**Files:**
- Create: `backend/Ecommerce/Controllers/AdminAuthController.cs`

**Interfaces:**
- Consumes: `IAdminAuthService` (Task 7).
- Produces: `POST api/Admin/Auth/login`, `POST api/Admin/Auth/refresh`, `POST api/Admin/Auth/logout` — the frontend `AdminAuthServices` (Task 12) calls these three by exact path.

- [ ] **Step 1: Write the controller**

```csharp
// backend/Ecommerce/Controllers/AdminAuthController.cs
using Ecommerce.Contracts.AdminAuth;
using Ecommerce.Contracts.Common;
using System.Security.Claims;

namespace Ecommerce.Controllers;

// Explicit literal route, not "api/Admin/[controller]" — the [controller] token
// would resolve to "AdminAuth" (class name minus "Controller"), not "Auth".
[Route("api/Admin/Auth")]
[ApiController]
public class AdminAuthController(IAdminAuthService adminAuthService) : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService = adminAuthService;

    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminAuthService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Login failed."));

        return Ok(new ApiResponse<AdminAuthResponse>(StatusCodes.Status200OK, "Login successful.", result.Value));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync([FromBody] AdminRefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminAuthService.RefreshTokenAsync(request.Token, request.RefreshToken, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Refresh failed."));

        return Ok(new ApiResponse<AdminAuthResponse>(StatusCodes.Status200OK, "Token refreshed.", result.Value));
    }

    [Authorize(AuthenticationSchemes = Ecommerce.Authorization.AdminAuthDefaults.Scheme)]
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (adminId is null || !long.TryParse(adminId, out var id))
            return Unauthorized(new ApiResponse<object>(StatusCodes.Status401Unauthorized, "Authentication is required."));

        await _adminAuthService.LogoutAsync(id, cancellationToken);
        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Logged out."));
    }
}
```

- [ ] **Step 2: Build and run**

```powershell
dotnet build backend/Ecommerce.slnx
dotnet run --project backend/Ecommerce
```

- [ ] **Step 3: Manually verify login with the seeded admin**

From a separate terminal (adjust for your shell — `curl.exe` ships with Windows 10/11):

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin.tester@example.com\",\"password\":\"AdminTester@123\"}"
```

Expected: `200 OK`, JSON body with `data.roleName = "Super Admin"`, `data.permissions` containing all 13 keys from `PermissionKeys.Catalog`, and a non-empty `data.token`/`data.refreshToken`.

- [ ] **Step 4: Manually verify wrong password is rejected**

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin.tester@example.com\",\"password\":\"wrong\"}"
```

Expected: `400 Bad Request`.

- [ ] **Step 5: Manually verify refresh and logout**

Using the `token`/`refreshToken` from Step 3's response:

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Auth/refresh \
  -H "Content-Type: application/json" \
  -d "{\"token\":\"<token>\",\"refreshToken\":\"<refreshToken>\"}"
```

Expected: `200 OK` with a new token pair.

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Auth/logout \
  -H "Authorization: Bearer <token-from-step-3-or-5>"
```

Expected: `200 OK`. Re-running the same `refresh` call from Step 5 with the now-revoked refresh token should return `400 Bad Request`.

- [ ] **Step 6: Verify a customer token cannot call admin endpoints**

Log in as the customer seed user (`POST api/Auth/login` with `seed.tester@example.com` / `SeedTester@123` from the existing `DataSeeder`), then call `POST api/Admin/Auth/logout` with that customer's token in the `Authorization` header.

Expected: `401 Unauthorized` — the token's audience doesn't match `AdminBearer`'s configured `ValidAudience`.

- [ ] **Step 7: Commit**

```bash
git add backend/Ecommerce/Controllers/AdminAuthController.cs
git commit -m "Add AdminAuthController (login, refresh, logout)"
```

---

## Task 9: Password reset (forgot-password / reset-password)

**Files:**
- Create: `backend/Ecommerce/Contracts/AdminAuth/AdminForgotPasswordRequest.cs`
- Create: `backend/Ecommerce/Contracts/AdminAuth/AdminResetPasswordRequest.cs`
- Create: `backend/Ecommerce/Options/FrontendOptions.cs`
- Modify: `backend/Ecommerce/appsettings.json` (add `Frontend:AdminAppUrl`)
- Modify: `backend/Ecommerce/Errors/AdminAuthErrors.cs` (add `InvalidResetToken`)
- Modify: `backend/Ecommerce/Services/IAdminAuthService.cs` (add two methods)
- Modify: `backend/Ecommerce/Services/AdminAuthService.cs` (add `IEmailSender`/`IOptions<FrontendOptions>` dependencies + implementations)
- Modify: `backend/Ecommerce/Controllers/AdminAuthController.cs` (add two actions)
- Modify: `backend/Ecommerce/DependacyInjection.cs` (bind `FrontendOptions`)
- Modify: `backend/Ecommerce.Tests/Services/AdminAuthServiceTests.cs` (update constructor calls, add new tests)

**Interfaces:**
- Consumes: `IEmailSender` (Task 6).
- Produces: `IAdminAuthService.ForgotPasswordAsync(string email, CancellationToken) -> Task<Result>` (always succeeds if the request itself is well-formed — no enumeration signal), `.ResetPasswordAsync(string email, string token, string newPassword, CancellationToken) -> Task<Result>`. `POST api/Admin/Auth/forgot-password`, `POST api/Admin/Auth/reset-password` — Task 17's frontend forgot/reset pages call these.

- [ ] **Step 1: Write the contracts, options, and error**

```csharp
// backend/Ecommerce/Contracts/AdminAuth/AdminForgotPasswordRequest.cs
namespace Ecommerce.Contracts.AdminAuth;

public record AdminForgotPasswordRequest(string Email);

public class AdminForgotPasswordRequestValidator : AbstractValidator<AdminForgotPasswordRequest>
{
    public AdminForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
```

```csharp
// backend/Ecommerce/Contracts/AdminAuth/AdminResetPasswordRequest.cs
namespace Ecommerce.Contracts.AdminAuth;

public record AdminResetPasswordRequest(string Email, string Token, string NewPassword);

public class AdminResetPasswordRequestValidator : AbstractValidator<AdminResetPasswordRequest>
{
    public AdminResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}
```

```csharp
// backend/Ecommerce/Options/FrontendOptions.cs
using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Options;

public class FrontendOptions
{
    public static string SectionName = "Frontend";

    [Required]
    public string AdminAppUrl { get; init; } = string.Empty;
}
```

In `backend/Ecommerce/appsettings.json`, add a top-level section:

```json
  "Frontend": {
    "AdminAppUrl": "http://localhost:4200/admin"
  },
```

In `backend/Ecommerce/Errors/AdminAuthErrors.cs`, add:

```csharp
    public static readonly Error InvalidResetToken = new("AdminAuth.InvalidResetToken", "This password reset link is invalid or has expired");
```

- [ ] **Step 2: Update the interface**

```csharp
// backend/Ecommerce/Services/IAdminAuthService.cs — add to the interface
    Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Write the failing tests**

Append to `backend/Ecommerce.Tests/Services/AdminAuthServiceTests.cs`. First update every existing `new AdminAuthService(context, jwtProvider.Object)` call to also pass an email sender mock and frontend options:

```csharp
    private static Mock<IEmailSender> CreateEmailSenderMock() => new();

    private static IOptions<FrontendOptions> CreateFrontendOptions() =>
        Microsoft.Extensions.Options.Options.Create(new FrontendOptions { AdminAppUrl = "http://localhost:4200/admin" });

    private static AdminAuthService CreateService(ApplicationDbContext context, Mock<IAdminJwtProvider>? jwtProvider = null, Mock<IEmailSender>? emailSender = null) =>
        new(context, (jwtProvider ?? CreateJwtProviderMock()).Object, (emailSender ?? CreateEmailSenderMock()).Object, CreateFrontendOptions());
```

Replace every `new AdminAuthService(context, jwtProvider.Object)` / `new AdminAuthService(context, CreateJwtProviderMock().Object)` call in the existing four tests with `CreateService(context, jwtProvider)` / `CreateService(context)` respectively (same behavior, now going through the shared helper). Add:

```csharp
    [Fact]
    public async Task ForgotPasswordAsync_sends_a_reset_email_for_a_known_address()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "Correct#123");
        var emailSender = CreateEmailSenderMock();
        var service = CreateService(context, emailSender: emailSender);

        var result = await service.ForgotPasswordAsync("test.admin@example.com");

        Assert.True(result.IsSuccess);
        emailSender.Verify(x => x.SendAsync(
            "test.admin@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("http://localhost:4200/admin")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_succeeds_silently_for_an_unknown_address()
    {
        await using var context = CreateContext();
        var emailSender = CreateEmailSenderMock();
        var service = CreateService(context, emailSender: emailSender);

        var result = await service.ForgotPasswordAsync("nobody@example.com");

        Assert.True(result.IsSuccess);
        emailSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_updates_the_password_and_revokes_refresh_tokens_for_a_valid_token()
    {
        await using var context = CreateContext();
        var admin = await SeedAdminAsync(context, "OldPass#123");
        admin.RefreshTokens.Add(new AdminRefreshToken { Token = "rt-1", ExpiresOn = DateTime.UtcNow.AddDays(14) });
        await context.SaveChangesAsync();
        var service = CreateService(context);
        await service.ForgotPasswordAsync("test.admin@example.com");
        var issuedToken = (await context.Admins.FirstAsync(x => x.Id == admin.Id)).PasswordResetTokens.Single().Token;

        var result = await service.ResetPasswordAsync("test.admin@example.com", issuedToken, "NewPass#456");

        Assert.True(result.IsSuccess);
        var reloaded = await context.Admins.FirstAsync(x => x.Id == admin.Id);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<Admin>().VerifyHashedPassword(reloaded, reloaded.PasswordHash, "NewPass#456"));
        Assert.All(reloaded.RefreshTokens, rt => Assert.False(rt.IsActive));
    }

    [Fact]
    public async Task ResetPasswordAsync_fails_for_an_unknown_or_reused_token()
    {
        await using var context = CreateContext();
        await SeedAdminAsync(context, "OldPass#123");
        var service = CreateService(context);

        var result = await service.ResetPasswordAsync("test.admin@example.com", "not-a-real-token", "NewPass#456");

        Assert.False(result.IsSuccess);
        Assert.Equal("AdminAuth.InvalidResetToken", result.Error.Code);
    }
```

- [ ] **Step 4: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminAuthServiceTests
```

Expected: FAIL — `ForgotPasswordAsync`/`ResetPasswordAsync` don't exist yet.

- [ ] **Step 5: Implement `ForgotPasswordAsync`/`ResetPasswordAsync`**

Update the `AdminAuthService` constructor and add the two methods:

```csharp
// backend/Ecommerce/Services/AdminAuthService.cs — constructor line
public class AdminAuthService(
    ApplicationDbContext context,
    IAdminJwtProvider jwtProvider,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions) : IAdminAuthService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IAdminJwtProvider _jwtProvider = jwtProvider;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;
    private readonly PasswordHasher<Admin> _passwordHasher = new();
    private readonly int _refreshTokenExpiryDays = 14;
    private readonly int _resetTokenExpiryMinutes = 60;

    // ...LoginAsync, RefreshTokenAsync, LogoutAsync, IssueTokensAsync unchanged...

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (admin is null)
            return Result.Success(); // no enumeration signal

        var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        admin.PasswordResetTokens.Add(new AdminPasswordResetToken
        {
            Token = token,
            ExpiresOn = DateTime.UtcNow.AddMinutes(_resetTokenExpiryMinutes)
        });
        await _context.SaveChangesAsync(cancellationToken);

        var resetLink = $"{_frontendOptions.AdminAppUrl}/auth/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        await _emailSender.SendAsync(
            email,
            "Reset your ShopDemo admin password",
            $"<p>Click the link below to set a new password. This link expires in {_resetTokenExpiryMinutes} minutes.</p><p><a href=\"{resetLink}\">{resetLink}</a></p>",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        var resetToken = admin?.PasswordResetTokens.SingleOrDefault(x => x.Token == token && x.IsUsable);

        if (admin is null || resetToken is null)
            return Result.Failure(AdminAuthErrors.InvalidResetToken);

        admin.PasswordHash = _passwordHasher.HashPassword(admin, newPassword);
        resetToken.UsedOn = DateTime.UtcNow;

        foreach (var refreshToken in admin.RefreshTokens.Where(x => x.IsActive))
            refreshToken.RevokedOn = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
```

Add `using Ecommerce.Email;` and `using Ecommerce.Options;` to the top of `AdminAuthService.cs`.

- [ ] **Step 6: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminAuthServiceTests
```

Expected: 8 passed.

- [ ] **Step 7: Add the controller actions**

```csharp
// backend/Ecommerce/Controllers/AdminAuthController.cs — add inside the class
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] AdminForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _adminAuthService.ForgotPasswordAsync(request.Email, cancellationToken);
        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "If that email is registered, a reset link has been sent."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] AdminResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminAuthService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Reset failed."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Password updated. You can now log in."));
    }
```

- [ ] **Step 8: Bind `FrontendOptions`**

In `backend/Ecommerce/DependacyInjection.cs`, alongside the `SmtpOptions` binding added in Task 6:

```csharp
            services.AddOptions<FrontendOptions>()
                .BindConfiguration(FrontendOptions.SectionName)
                .ValidateOnStart();
```

Add `using Ecommerce.Options;` to the top of `DependacyInjection.cs`.

- [ ] **Step 9: Manually verify the real Mailtrap send**

```powershell
dotnet run --project backend/Ecommerce
```

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Auth/forgot-password \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin.tester@example.com\"}"
```

Expected: `200 OK`, and within a few seconds a new email appears in the Mailtrap sandbox inbox with subject "Reset your ShopDemo admin password" containing a `http://localhost:4200/admin/auth/reset-password?email=...&token=...` link. Copy the `token` query value, then:

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Auth/reset-password \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin.tester@example.com\",\"token\":\"<token-from-email>\",\"newPassword\":\"AdminTester@456\"}"
```

Expected: `200 OK`. Log in with the new password to confirm it took effect, then reset it back to `AdminTester@123` (repeat the flow) so later tasks' manual checks keep working with the documented seed credentials.

- [ ] **Step 10: Commit**

```bash
git add backend/Ecommerce/Contracts/AdminAuth backend/Ecommerce/Options backend/Ecommerce/appsettings.json backend/Ecommerce/Errors/AdminAuthErrors.cs backend/Ecommerce/Services backend/Ecommerce/Controllers/AdminAuthController.cs backend/Ecommerce/DependacyInjection.cs backend/Ecommerce.Tests/Services
git commit -m "Add admin password reset (forgot-password/reset-password) via email"
```

---

## Task 10: Roles & Permissions CRUD

**Files:**
- Create: `backend/Ecommerce/Contracts/Roles/PermissionResponse.cs`
- Create: `backend/Ecommerce/Contracts/Roles/RoleRequest.cs`
- Create: `backend/Ecommerce/Contracts/Roles/RoleResponse.cs`
- Create: `backend/Ecommerce/Errors/RoleErrors.cs`
- Create: `backend/Ecommerce/Services/IRoleService.cs`
- Create: `backend/Ecommerce/Services/RoleService.cs`
- Create: `backend/Ecommerce/Controllers/RolesController.cs`
- Modify: `backend/Ecommerce/DependacyInjection.cs` (register `IRoleService`)
- Test: `backend/Ecommerce.Tests/Services/RoleServiceTests.cs`

**Interfaces:**
- Consumes: `AdminRole`/`Permission` (Task 2), `PermissionKeys` (Task 2), `[HasPermission]` (Task 5).
- Produces: `GET/POST/PUT/DELETE api/Admin/Roles[/{id}]`, `GET api/Admin/Permissions` — Task 19's frontend Roles page calls all four plus the catalog endpoint. `RoleResponse { Id, Name, Description?, IsSystem, Permissions: PermissionResponse[] }`, `PermissionResponse { Id, Key, Module, Description }`.

- [ ] **Step 1: Write the contracts and errors**

```csharp
// backend/Ecommerce/Contracts/Roles/PermissionResponse.cs
namespace Ecommerce.Contracts.Roles;

public record PermissionResponse(long Id, string Key, string Module, string Description);
```

```csharp
// backend/Ecommerce/Contracts/Roles/RoleRequest.cs
namespace Ecommerce.Contracts.Roles;

public record RoleRequest(string Name, string? Description, List<string> PermissionKeys);

public class RoleRequestValidator : AbstractValidator<RoleRequest>
{
    public RoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.PermissionKeys).NotNull();
    }
}
```

```csharp
// backend/Ecommerce/Contracts/Roles/RoleResponse.cs
namespace Ecommerce.Contracts.Roles;

public record RoleResponse(long Id, string Name, string? Description, bool IsSystem, List<PermissionResponse> Permissions);
```

```csharp
// backend/Ecommerce/Errors/RoleErrors.cs
namespace Ecommerce.Errors;

public static class RoleErrors
{
    public static readonly Error RoleNotFound = new("Role.NotFound", "No role was found with the given ID");
    public static readonly Error RoleNameExists = new("Role.NameExists", "Another role with the same name already exists");
    public static readonly Error SystemRoleProtected = new("Role.SystemRoleProtected", "Built-in system roles cannot be edited or deleted");
    public static readonly Error UnknownPermissionKey = new("Role.UnknownPermissionKey", "One or more permission keys do not exist");
    public static readonly Error RoleInUse = new("Role.InUse", "This role is still assigned to one or more admins");
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// backend/Ecommerce.Tests/Services/RoleServiceTests.cs
using Ecommerce.Contracts.Roles;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class RoleServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task SeedPermissionsAsync(ApplicationDbContext context)
    {
        context.Permissions.AddRange(
            new Permission { Key = "products.manage", Module = "Products", Description = "Manage products" },
            new Permission { Key = "orders.view", Module = "Orders", Description = "View orders" });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAsync_creates_a_role_with_the_requested_permissions()
    {
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var service = new RoleService(context);

        var result = await service.CreateAsync(new RoleRequest("Editor", "Can edit products", ["products.manage"]));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Permissions);
        Assert.Equal("products.manage", result.Value.Permissions[0].Key);
    }

    [Fact]
    public async Task CreateAsync_fails_for_a_duplicate_name()
    {
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var service = new RoleService(context);
        await service.CreateAsync(new RoleRequest("Editor", null, []));

        var result = await service.CreateAsync(new RoleRequest("Editor", null, []));

        Assert.False(result.IsSuccess);
        Assert.Equal("Role.NameExists", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_fails_for_an_unknown_permission_key()
    {
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var service = new RoleService(context);

        var result = await service.CreateAsync(new RoleRequest("Editor", null, ["not.a.real.permission"]));

        Assert.False(result.IsSuccess);
        Assert.Equal("Role.UnknownPermissionKey", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_replaces_the_permission_set()
    {
        await using var context = CreateContext();
        await SeedPermissionsAsync(context);
        var service = new RoleService(context);
        var created = (await service.CreateAsync(new RoleRequest("Editor", null, ["products.manage"]))).Value;

        var result = await service.UpdateAsync(created.Id, new RoleRequest("Editor", null, ["orders.view"]));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Permissions);
        Assert.Equal("orders.view", result.Value.Permissions[0].Key);
    }

    [Fact]
    public async Task DeleteAsync_fails_for_a_system_role()
    {
        await using var context = CreateContext();
        context.AdminRoles.Add(new AdminRole { Name = "Super Admin", IsSystem = true });
        await context.SaveChangesAsync();
        var systemRole = await context.AdminRoles.FirstAsync();
        var service = new RoleService(context);

        var result = await service.DeleteAsync(systemRole.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Role.SystemRoleProtected", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_fails_when_the_role_is_still_assigned_to_an_admin()
    {
        await using var context = CreateContext();
        var role = new AdminRole { Name = "Editor" };
        context.AdminRoles.Add(role);
        context.Admins.Add(new Admin { FirstName = "A", LastName = "B", Email = "a@example.com", PasswordHash = "x", AdminRole = role });
        await context.SaveChangesAsync();
        var service = new RoleService(context);

        var result = await service.DeleteAsync(role.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Role.InUse", result.Error.Code);
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter RoleServiceTests
```

Expected: FAIL — `IRoleService`/`RoleService` don't exist yet.

- [ ] **Step 4: Write `IRoleService` and `RoleService`**

```csharp
// backend/Ecommerce/Services/IRoleService.cs
using Ecommerce.Contracts.Roles;

namespace Ecommerce.Services;

public interface IRoleService
{
    Task<Result<IEnumerable<RoleResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<RoleResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<IEnumerable<PermissionResponse>>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default);
    Task<Result<RoleResponse>> CreateAsync(RoleRequest request, CancellationToken cancellationToken = default);
    Task<Result<RoleResponse>> UpdateAsync(long id, RoleRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
```

```csharp
// backend/Ecommerce/Services/RoleService.cs
using Ecommerce.Contracts.Roles;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Services;

public class RoleService(ApplicationDbContext context) : IRoleService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Result<IEnumerable<RoleResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _context.AdminRoles.Include(x => x.Permissions).AsNoTracking().ToListAsync(cancellationToken);
        return Result.Success<IEnumerable<RoleResponse>>(roles.Select(MapRole).ToList());
    }

    public async Task<Result<RoleResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var role = await _context.AdminRoles.Include(x => x.Permissions).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return role is null ? Result.Failure<RoleResponse>(RoleErrors.RoleNotFound) : Result.Success(MapRole(role));
    }

    public async Task<Result<IEnumerable<PermissionResponse>>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await _context.Permissions.AsNoTracking().OrderBy(x => x.Module).ThenBy(x => x.Key).ToListAsync(cancellationToken);
        return Result.Success<IEnumerable<PermissionResponse>>(permissions.Select(MapPermission).ToList());
    }

    public async Task<Result<RoleResponse>> CreateAsync(RoleRequest request, CancellationToken cancellationToken = default)
    {
        if (await _context.AdminRoles.AnyAsync(x => x.Name == request.Name, cancellationToken))
            return Result.Failure<RoleResponse>(RoleErrors.RoleNameExists);

        var permissionsResult = await ResolvePermissionsAsync(request.PermissionKeys, cancellationToken);
        if (!permissionsResult.IsSuccess)
            return Result.Failure<RoleResponse>(permissionsResult.Error);

        var role = new AdminRole { Name = request.Name, Description = request.Description, Permissions = permissionsResult.Value };
        _context.AdminRoles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapRole(role));
    }

    public async Task<Result<RoleResponse>> UpdateAsync(long id, RoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _context.AdminRoles.Include(x => x.Permissions).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
            return Result.Failure<RoleResponse>(RoleErrors.RoleNotFound);

        if (role.IsSystem)
            return Result.Failure<RoleResponse>(RoleErrors.SystemRoleProtected);

        if (await _context.AdminRoles.AnyAsync(x => x.Id != id && x.Name == request.Name, cancellationToken))
            return Result.Failure<RoleResponse>(RoleErrors.RoleNameExists);

        var permissionsResult = await ResolvePermissionsAsync(request.PermissionKeys, cancellationToken);
        if (!permissionsResult.IsSuccess)
            return Result.Failure<RoleResponse>(permissionsResult.Error);

        role.Name = request.Name;
        role.Description = request.Description;
        role.Permissions = permissionsResult.Value;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapRole(role));
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var role = await _context.AdminRoles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (role is null)
            return Result.Failure(RoleErrors.RoleNotFound);

        if (role.IsSystem)
            return Result.Failure(RoleErrors.SystemRoleProtected);

        if (await _context.Admins.AnyAsync(x => x.AdminRoleId == id, cancellationToken))
            return Result.Failure(RoleErrors.RoleInUse);

        _context.AdminRoles.Remove(role);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<List<Permission>>> ResolvePermissionsAsync(List<string> keys, CancellationToken cancellationToken)
    {
        var distinctKeys = keys.Distinct().ToList();
        var permissions = await _context.Permissions.Where(x => distinctKeys.Contains(x.Key)).ToListAsync(cancellationToken);

        return permissions.Count != distinctKeys.Count
            ? Result.Failure<List<Permission>>(RoleErrors.UnknownPermissionKey)
            : Result.Success(permissions);
    }

    private static RoleResponse MapRole(AdminRole role) => new(
        role.Id, role.Name, role.Description, role.IsSystem, role.Permissions.Select(MapPermission).ToList());

    private static PermissionResponse MapPermission(Permission permission) => new(
        permission.Id, permission.Key, permission.Module, permission.Description);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter RoleServiceTests
```

Expected: 6 passed.

- [ ] **Step 6: Write the controller**

```csharp
// backend/Ecommerce/Controllers/RolesController.cs
using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Roles;

namespace Ecommerce.Controllers;

[HasPermission(PermissionKeys.RolesManage)]
[Route("api/Admin/[controller]")]
[ApiController]
public class RolesController(IRoleService roleService) : ControllerBase
{
    private readonly IRoleService _roleService = roleService;

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _roleService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<RoleResponse>>(StatusCodes.Status200OK, "Roles loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _roleService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Role not found."));

        return Ok(new ApiResponse<RoleResponse>(StatusCodes.Status200OK, "Role loaded.", result.Value));
    }

    [HttpGet("~/api/Admin/Permissions")]
    public async Task<IActionResult> GetPermissionCatalogAsync(CancellationToken cancellationToken)
    {
        var result = await _roleService.GetPermissionCatalogAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<PermissionResponse>>(StatusCodes.Status200OK, "Permissions loaded.", result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create role."));

        var response = new ApiResponse<RoleResponse>(StatusCodes.Status201Created, "Role created.", result.Value);
        return Created($"/api/Admin/Roles/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update role."));

        return Ok(new ApiResponse<RoleResponse>(StatusCodes.Status200OK, "Role updated.", result.Value));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _roleService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete role."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Role deleted."));
    }
}
```

- [ ] **Step 7: Register the service**

In `backend/Ecommerce/DependacyInjection.cs`:

```csharp
            services.AddScoped<IRoleService, RoleService>();
```

- [ ] **Step 8: Manually verify against the seeded Super Admin**

```powershell
dotnet build backend/Ecommerce.slnx
dotnet run --project backend/Ecommerce
```

Log in as `admin.tester@example.com` (Task 8, Step 3) to get a token, then:

```bash
curl.exe -k https://localhost:7297/api/Admin/Permissions -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Admin/Roles -H "Authorization: Bearer <token>"
```

Expected: `200 OK` on both; `Roles` includes `"Super Admin"` with all 13 permissions. Retry both calls with no `Authorization` header — expect `401 Unauthorized`.

- [ ] **Step 9: Commit**

```bash
git add backend/Ecommerce/Contracts/Roles backend/Ecommerce/Errors/RoleErrors.cs backend/Ecommerce/Services/IRoleService.cs backend/Ecommerce/Services/RoleService.cs backend/Ecommerce/Controllers/RolesController.cs backend/Ecommerce/DependacyInjection.cs backend/Ecommerce.Tests/Services/RoleServiceTests.cs
git commit -m "Add Roles & Permissions CRUD"
```

---

## Task 11: Admins CRUD

**Files:**
- Create: `backend/Ecommerce/Contracts/Admins/AdminResponse.cs`
- Create: `backend/Ecommerce/Contracts/Admins/CreateAdminRequest.cs`
- Create: `backend/Ecommerce/Contracts/Admins/UpdateAdminRequest.cs`
- Create: `backend/Ecommerce/Contracts/Admins/SetAdminStatusRequest.cs`
- Create: `backend/Ecommerce/Errors/AdminErrors.cs`
- Create: `backend/Ecommerce/Services/IAdminService.cs`
- Create: `backend/Ecommerce/Services/AdminService.cs`
- Create: `backend/Ecommerce/Controllers/AdminsController.cs`
- Modify: `backend/Ecommerce/DependacyInjection.cs` (register `IAdminService`)
- Test: `backend/Ecommerce.Tests/Services/AdminServiceTests.cs`

**Interfaces:**
- Consumes: `Admin`/`AdminRole` (Task 2), `IAdminAuthService.ForgotPasswordAsync` (Task 9, reused so a newly-created admin gets a "set your password" email through the exact same token/email path instead of a second implementation), `[HasPermission]` (Task 5).
- Produces: `GET/POST/PUT/DELETE api/Admin/Admins[/{id}]`, `PUT api/Admin/Admins/{id}/status` — Task 20's frontend Admins page calls all five. `AdminResponse { Id, FirstName, LastName, Email, PhoneNumber?, RoleId, RoleName, IsActive, CreatedOn }`.

- [ ] **Step 1: Write the contracts and errors**

```csharp
// backend/Ecommerce/Contracts/Admins/AdminResponse.cs
namespace Ecommerce.Contracts.Admins;

public record AdminResponse(
    long Id, string FirstName, string LastName, string Email, string? PhoneNumber,
    long RoleId, string RoleName, bool IsActive, DateTime CreatedOn);
```

```csharp
// backend/Ecommerce/Contracts/Admins/CreateAdminRequest.cs
namespace Ecommerce.Contracts.Admins;

public record CreateAdminRequest(string FirstName, string LastName, string Email, string? PhoneNumber, long RoleId);

public class CreateAdminRequestValidator : AbstractValidator<CreateAdminRequest>
{
    public CreateAdminRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.RoleId).GreaterThan(0);
    }
}
```

```csharp
// backend/Ecommerce/Contracts/Admins/UpdateAdminRequest.cs
namespace Ecommerce.Contracts.Admins;

public record UpdateAdminRequest(string FirstName, string LastName, string? PhoneNumber, long RoleId, bool IsActive);

public class UpdateAdminRequestValidator : AbstractValidator<UpdateAdminRequest>
{
    public UpdateAdminRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.RoleId).GreaterThan(0);
    }
}
```

```csharp
// backend/Ecommerce/Contracts/Admins/SetAdminStatusRequest.cs
namespace Ecommerce.Contracts.Admins;

public record SetAdminStatusRequest(bool IsActive);
```

```csharp
// backend/Ecommerce/Errors/AdminErrors.cs
namespace Ecommerce.Errors;

public static class AdminErrors
{
    public static readonly Error AdminNotFound = new("Admin.NotFound", "No admin was found with the given ID");
    public static readonly Error EmailAlreadyExists = new("Admin.EmailAlreadyExists", "Another admin with this email already exists");
    public static readonly Error RoleNotFound = new("Admin.RoleNotFound", "The selected role does not exist");
    public static readonly Error CannotModifyOwnAccount = new("Admin.CannotModifyOwnAccount", "You cannot deactivate or delete your own account");
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// backend/Ecommerce.Tests/Services/AdminServiceTests.cs
using Ecommerce.Contracts.Admins;
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Ecommerce.Tests.Services;

public class AdminServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<AdminRole> SeedRoleAsync(ApplicationDbContext context, string name = "Manager")
    {
        var role = new AdminRole { Name = name };
        context.AdminRoles.Add(role);
        await context.SaveChangesAsync();
        return role;
    }

    private static async Task<Admin> SeedAdminAsync(ApplicationDbContext context, AdminRole role, string email = "existing@example.com")
    {
        var admin = new Admin { FirstName = "Existing", LastName = "Admin", Email = email, PasswordHash = "x", AdminRole = role };
        context.Admins.Add(admin);
        await context.SaveChangesAsync();
        return admin;
    }

    [Fact]
    public async Task CreateAsync_creates_the_admin_and_sends_a_set_password_email()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var authService = new Mock<IAdminAuthService>();
        authService.Setup(x => x.ForgotPasswordAsync("new.admin@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var service = new AdminService(context, authService.Object);

        var result = await service.CreateAsync(new CreateAdminRequest("New", "Admin", "new.admin@example.com", null, role.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(role.Name, result.Value.RoleName);
        authService.Verify(x => x.ForgotPasswordAsync("new.admin@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_fails_for_a_duplicate_email()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        await SeedAdminAsync(context, role, "dup@example.com");
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.CreateAsync(new CreateAdminRequest("New", "Admin", "dup@example.com", null, role.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.EmailAlreadyExists", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_fails_for_an_unknown_role()
    {
        await using var context = CreateContext();
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.CreateAsync(new CreateAdminRequest("New", "Admin", "new.admin@example.com", null, 999));

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.RoleNotFound", result.Error.Code);
    }

    [Fact]
    public async Task SetStatusAsync_fails_when_an_admin_tries_to_deactivate_themselves()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var admin = await SeedAdminAsync(context, role);
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.SetStatusAsync(admin.Id, false, currentAdminId: admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.CannotModifyOwnAccount", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_fails_when_an_admin_tries_to_delete_themselves()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var admin = await SeedAdminAsync(context, role);
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.DeleteAsync(admin.Id, currentAdminId: admin.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("Admin.CannotModifyOwnAccount", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_succeeds_for_a_different_admin()
    {
        await using var context = CreateContext();
        var role = await SeedRoleAsync(context);
        var admin = await SeedAdminAsync(context, role);
        var service = new AdminService(context, new Mock<IAdminAuthService>().Object);

        var result = await service.DeleteAsync(admin.Id, currentAdminId: admin.Id + 1);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Admins.AnyAsync(x => x.Id == admin.Id));
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminServiceTests
```

Expected: FAIL — `IAdminService`/`AdminService` don't exist yet.

- [ ] **Step 4: Write `IAdminService` and `AdminService`**

```csharp
// backend/Ecommerce/Services/IAdminService.cs
using Ecommerce.Contracts.Admins;

namespace Ecommerce.Services;

public interface IAdminService
{
    Task<Result<IEnumerable<AdminResponse>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Result<AdminResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<AdminResponse>> CreateAsync(CreateAdminRequest request, CancellationToken cancellationToken = default);
    Task<Result<AdminResponse>> UpdateAsync(long id, UpdateAdminRequest request, CancellationToken cancellationToken = default);
    Task<Result> SetStatusAsync(long id, bool isActive, long currentAdminId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(long id, long currentAdminId, CancellationToken cancellationToken = default);
}
```

```csharp
// backend/Ecommerce/Services/AdminService.cs
using Ecommerce.Contracts.Admins;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Services;

public class AdminService(ApplicationDbContext context, IAdminAuthService adminAuthService) : IAdminService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IAdminAuthService _adminAuthService = adminAuthService;

    public async Task<Result<IEnumerable<AdminResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var admins = await _context.Admins.Include(x => x.AdminRole).AsNoTracking().ToListAsync(cancellationToken);
        return Result.Success<IEnumerable<AdminResponse>>(admins.Select(MapAdmin).ToList());
    }

    public async Task<Result<AdminResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.Include(x => x.AdminRole).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return admin is null ? Result.Failure<AdminResponse>(AdminErrors.AdminNotFound) : Result.Success(MapAdmin(admin));
    }

    public async Task<Result<AdminResponse>> CreateAsync(CreateAdminRequest request, CancellationToken cancellationToken = default)
    {
        if (await _context.Admins.AnyAsync(x => x.Email == request.Email, cancellationToken))
            return Result.Failure<AdminResponse>(AdminErrors.EmailAlreadyExists);

        var role = await _context.AdminRoles.FirstOrDefaultAsync(x => x.Id == request.RoleId, cancellationToken);
        if (role is null)
            return Result.Failure<AdminResponse>(AdminErrors.RoleNotFound);

        var admin = new Admin
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            AdminRoleId = role.Id,
            AdminRole = role,
            IsActive = true,
        };
        // Unusable placeholder — nobody, including the creating admin, ever knows this
        // value. The new admin sets a real password themselves via the emailed link.
        admin.PasswordHash = new PasswordHasher<Admin>().HashPassword(admin, Guid.NewGuid().ToString());

        _context.Admins.Add(admin);
        await _context.SaveChangesAsync(cancellationToken);

        await _adminAuthService.ForgotPasswordAsync(admin.Email, cancellationToken);

        return Result.Success(MapAdmin(admin));
    }

    public async Task<Result<AdminResponse>> UpdateAsync(long id, UpdateAdminRequest request, CancellationToken cancellationToken = default)
    {
        var admin = await _context.Admins.Include(x => x.AdminRole).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (admin is null)
            return Result.Failure<AdminResponse>(AdminErrors.AdminNotFound);

        var role = await _context.AdminRoles.FirstOrDefaultAsync(x => x.Id == request.RoleId, cancellationToken);
        if (role is null)
            return Result.Failure<AdminResponse>(AdminErrors.RoleNotFound);

        admin.FirstName = request.FirstName;
        admin.LastName = request.LastName;
        admin.PhoneNumber = request.PhoneNumber;
        admin.AdminRoleId = role.Id;
        admin.AdminRole = role;
        admin.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(MapAdmin(admin));
    }

    public async Task<Result> SetStatusAsync(long id, bool isActive, long currentAdminId, CancellationToken cancellationToken = default)
    {
        if (id == currentAdminId)
            return Result.Failure(AdminErrors.CannotModifyOwnAccount);

        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (admin is null)
            return Result.Failure(AdminErrors.AdminNotFound);

        admin.IsActive = isActive;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, long currentAdminId, CancellationToken cancellationToken = default)
    {
        if (id == currentAdminId)
            return Result.Failure(AdminErrors.CannotModifyOwnAccount);

        var admin = await _context.Admins.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (admin is null)
            return Result.Failure(AdminErrors.AdminNotFound);

        _context.Admins.Remove(admin);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static AdminResponse MapAdmin(Admin admin) => new(
        admin.Id, admin.FirstName, admin.LastName, admin.Email, admin.PhoneNumber,
        admin.AdminRoleId, admin.AdminRole.Name, admin.IsActive, admin.CreatedOn);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter AdminServiceTests
```

Expected: 6 passed.

- [ ] **Step 6: Write the controller**

```csharp
// backend/Ecommerce/Controllers/AdminsController.cs
using Ecommerce.Authorization;
using Ecommerce.Contracts.Admins;
using Ecommerce.Contracts.Common;
using System.Security.Claims;

namespace Ecommerce.Controllers;

[HasPermission(PermissionKeys.AdminsManage)]
[Route("api/Admin/[controller]")]
[ApiController]
public class AdminsController(IAdminService adminService) : ControllerBase
{
    private readonly IAdminService _adminService = adminService;

    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _adminService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<AdminResponse>>(StatusCodes.Status200OK, "Admins loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _adminService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Admin not found."));

        return Ok(new ApiResponse<AdminResponse>(StatusCodes.Status200OK, "Admin loaded.", result.Value));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAdminRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create admin."));

        var response = new ApiResponse<AdminResponse>(StatusCodes.Status201Created, "Admin created. A set-password email has been sent.", result.Value);
        return Created($"/api/Admin/Admins/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromBody] UpdateAdminRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update admin."));

        return Ok(new ApiResponse<AdminResponse>(StatusCodes.Status200OK, "Admin updated.", result.Value));
    }

    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetStatusAsync([FromRoute] long id, [FromBody] SetAdminStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _adminService.SetStatusAsync(id, request.IsActive, GetCurrentAdminId(), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Admin status updated."));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _adminService.DeleteAsync(id, GetCurrentAdminId(), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete admin."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Admin deleted."));
    }

    private long GetCurrentAdminId() => long.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
```

- [ ] **Step 7: Register the service**

In `backend/Ecommerce/DependacyInjection.cs`:

```csharp
            services.AddScoped<IAdminService, AdminService>();
```

- [ ] **Step 8: Manually verify against the seeded Super Admin**

```powershell
dotnet build backend/Ecommerce.slnx
dotnet run --project backend/Ecommerce
```

Log in as `admin.tester@example.com`, then create a second admin, confirm a "set your password" email lands in Mailtrap, complete that flow via `reset-password` (Task 9, Step 9), and log in as the new admin:

```bash
curl.exe -k -X POST https://localhost:7297/api/Admin/Admins -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
  -d "{\"firstName\":\"Second\",\"lastName\":\"Admin\",\"email\":\"second.admin@example.com\",\"roleId\":<super-admin-role-id-from-Roles-list>}"
```

Then confirm the self-protection guard:

```bash
curl.exe -k -X DELETE https://localhost:7297/api/Admin/Admins/<your-own-id> -H "Authorization: Bearer <token>"
```

Expected: `400 Bad Request` with `Admin.CannotModifyOwnAccount`.

- [ ] **Step 9: Commit**

```bash
git add backend/Ecommerce/Contracts/Admins backend/Ecommerce/Errors/AdminErrors.cs backend/Ecommerce/Services/IAdminService.cs backend/Ecommerce/Services/AdminService.cs backend/Ecommerce/Controllers/AdminsController.cs backend/Ecommerce/DependacyInjection.cs backend/Ecommerce.Tests/Services/AdminServiceTests.cs
git commit -m "Add Admins CRUD"
```

This is the end of the backend. Backend recap: run the full suite once before moving to the frontend.

```powershell
dotnet test backend/Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: all tests passed (SmokeTests: 1, AdminModelTests: 1, AdminJwtProviderTests: 3, PermissionAuthorizationHandlerTests: 2, SmtpEmailSenderTests: 1, AdminAuthServiceTests: 8, RoleServiceTests: 6, AdminServiceTests: 6 — 28 total).

---

## Task 12: Frontend admin interfaces + `AdminAuthServices`

**Files:**
- Create: `frontend/src/app/admin/shared/interface/admin-auth-interfaces.ts`
- Create: `frontend/src/app/admin/core/services/admin-auth-services.ts`

**Interfaces:**
- Consumes: `POST/GET api/Admin/Auth/*` (Tasks 8–9).
- Produces: `AdminAuthServices` — `login()`, `logout()`, `refreshToken()`, `forgotPassword()`, `resetPassword()`, `user: Signal<AdminAuthResponseInterface | null>`, `isLoggedIn: Signal<boolean>`, `hasPermission(key: string): boolean` — Task 13's guards, Task 14's interceptor, Task 16/17's auth pages, and Task 19/20's Roles/Admins pages all depend on this exact API. Session stored under localStorage key `shopdemo_admin_auth` (never `shopdemo_auth` — that's the customer key).

- [ ] **Step 1: Write the interfaces**

```typescript
// frontend/src/app/admin/shared/interface/admin-auth-interfaces.ts
export interface AdminAuthResponseInterface {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  roleName: string;
  permissions: string[];
  token: string;
  expiresIn: number;
  refreshToken: string;
  refreshTokenExpiration: string;
}

export interface AdminApiEnvelope<T> {
  statusCode: number;
  message: string;
  data: T;
}
```

- [ ] **Step 2: Write `AdminAuthServices`**

```typescript
// frontend/src/app/admin/core/services/admin-auth-services.ts
import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, Observable, of } from 'rxjs';
import { AdminApiEnvelope, AdminAuthResponseInterface } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class AdminAuthServices {
  private http = inject(HttpClient);
  private readonly storageKey = 'shopdemo_admin_auth';

  readonly user = signal<AdminAuthResponseInterface | null>(this.readStoredAdmin());
  readonly isLoggedIn = computed(() => this.user() !== null);

  login(payload: { email: string; password: string }): Observable<AdminAuthResponseInterface> {
    return this.http.post<AdminApiEnvelope<AdminAuthResponseInterface>>('/Admin/Auth/login', payload).pipe(
      map(response => this.storeAdmin(response.data) as AdminAuthResponseInterface)
    );
  }

  logout(): void {
    const current = this.user();
    this.clearSession();

    if (!current) return;

    // Best-effort: revoke the refresh token server-side. The client-side
    // session is already cleared above regardless of whether this succeeds.
    this.http.post('/Admin/Auth/logout', {}).pipe(catchError(() => of(null))).subscribe();
  }

  refreshToken(): Observable<AdminAuthResponseInterface | null> {
    const current = this.user();
    if (!current) return of(null);

    return this.http.post<AdminApiEnvelope<AdminAuthResponseInterface>>('/Admin/Auth/refresh', {
      token: current.token,
      refreshToken: current.refreshToken,
    }).pipe(
      map(response => this.storeAdmin(response.data)),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<AdminApiEnvelope<unknown>>('/Admin/Auth/forgot-password', { email }).pipe(map(() => undefined));
  }

  resetPassword(payload: { email: string; token: string; newPassword: string }): Observable<void> {
    return this.http.post<AdminApiEnvelope<unknown>>('/Admin/Auth/reset-password', payload).pipe(map(() => undefined));
  }

  hasPermission(key: string): boolean {
    return this.user()?.permissions.includes(key) ?? false;
  }

  clearSession(): void {
    localStorage.removeItem(this.storageKey);
    this.user.set(null);
  }

  private storeAdmin(payload: AdminAuthResponseInterface | undefined | null): AdminAuthResponseInterface | null {
    if (!payload) {
      this.user.set(null);
      return null;
    }

    localStorage.setItem(this.storageKey, JSON.stringify(payload));
    this.user.set(payload);
    return payload;
  }

  private readStoredAdmin(): AdminAuthResponseInterface | null {
    if (typeof window === 'undefined') {
      return null;
    }

    const stored = localStorage.getItem(this.storageKey);
    return stored ? JSON.parse(stored) as AdminAuthResponseInterface : null;
  }
}
```

- [ ] **Step 3: Verify it compiles**

```powershell
cd frontend
npx tsc --noEmit -p tsconfig.app.json
```

Expected: no new errors referencing `admin-auth-services.ts` or `admin-auth-interfaces.ts`.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/app/admin/shared/interface/admin-auth-interfaces.ts frontend/src/app/admin/core/services/admin-auth-services.ts
git commit -m "Add frontend AdminAuthServices (login, logout, refresh, forgot/reset password)"
```

---

## Task 13: `adminAuthGuard` and `adminPermissionGuard`

**Files:**
- Create: `frontend/src/app/admin/core/guards/admin-auth-guard.ts`
- Create: `frontend/src/app/admin/core/guards/admin-permission-guard.ts`

**Interfaces:**
- Consumes: `AdminAuthServices.isLoggedIn`/`.hasPermission()` (Task 12).
- Produces: `adminAuthGuard: CanActivateFn` and `adminPermissionGuard(permission: string): CanActivateFn` — Task 18's routing uses both.

- [ ] **Step 1: Write `adminAuthGuard`**

```typescript
// frontend/src/app/admin/core/guards/admin-auth-guard.ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AdminAuthServices } from '../services/admin-auth-services';

export const adminAuthGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AdminAuthServices);
  const router = inject(Router);

  if (auth.isLoggedIn()) {
    return true;
  }

  return router.createUrlTree(['/admin/auth/login'], { queryParams: { returnUrl: state.url } });
};
```

- [ ] **Step 2: Write `adminPermissionGuard`**

```typescript
// frontend/src/app/admin/core/guards/admin-permission-guard.ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AdminAuthServices } from '../services/admin-auth-services';

export function adminPermissionGuard(permission: string): CanActivateFn {
  return () => {
    const auth = inject(AdminAuthServices);
    const router = inject(Router);

    return auth.hasPermission(permission) ? true : router.createUrlTree(['/admin']);
  };
}
```

- [ ] **Step 3: Verify it compiles**

```powershell
npx tsc --noEmit -p frontend/tsconfig.app.json
```

- [ ] **Step 4: Commit**

```bash
git add frontend/src/app/admin/core/guards
git commit -m "Add adminAuthGuard and adminPermissionGuard"
```

---

## Task 14: `api.interceptor.ts` — branch by `/Admin/` prefix

**Files:**
- Modify: `frontend/src/app/api.interceptor.ts`

**Interfaces:**
- Consumes: `AdminAuthServices` (Task 12).
- Produces: requests whose relative URL starts with `/Admin/` get the admin bearer token and, on a 401, retry once via `AdminAuthServices.refreshToken()` before redirecting to `/admin/auth/login`; every other request keeps today's customer-token behavior unchanged.

- [ ] **Step 1: Rewrite the interceptor**

```typescript
// frontend/src/app/api.interceptor.ts
import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { Environment } from '../environments/environment';
import { AccountServices } from './site/core/services/account-services';
import { AdminAuthServices } from './admin/core/services/admin-auth-services';

// Requests to these endpoints must never trigger a refresh-and-retry (it would
// either be pointless — login/register have no session yet — or recurse into
// the refresh call failing on itself).
const AUTH_ENDPOINTS_NO_RETRY = ['/Auth/login', '/Auth/register', '/Auth/refresh'];
const ADMIN_AUTH_ENDPOINTS_NO_RETRY = ['/Admin/Auth/login', '/Admin/Auth/refresh', '/Admin/Auth/forgot-password', '/Admin/Auth/reset-password'];

function readToken(key: string): string | null {
  try {
    if (typeof window === 'undefined') return null;
    const stored = localStorage.getItem(key);
    if (!stored) return null;
    return JSON.parse(stored)?.token ?? null;
  } catch {
    return null;
  }
}

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  // Pass through absolute URLs (external resources)
  if (req.url.startsWith('http://') || req.url.startsWith('https://')) {
    return next(req);
  }

  const isAdminRequest = req.url.startsWith('/Admin/');
  const account = inject(AccountServices);
  const adminAuth = inject(AdminAuthServices);
  const router = inject(Router);

  const apiUrl = `${Environment.apiUrl}${req.url}`;
  const token = isAdminRequest ? readToken('shopdemo_admin_auth') : readToken('shopdemo_auth');

  const apiReq = token
    ? req.clone({ url: apiUrl, setHeaders: { Authorization: `Bearer ${token}` } })
    : req.clone({ url: apiUrl });

  return next(apiReq).pipe(
    catchError((error: unknown) => {
      const noRetryList = isAdminRequest ? ADMIN_AUTH_ENDPOINTS_NO_RETRY : AUTH_ENDPOINTS_NO_RETRY;
      const canRetry = error instanceof HttpErrorResponse
        && error.status === 401
        && !noRetryList.some(endpoint => req.url.includes(endpoint));

      if (!canRetry) {
        return throwError(() => error);
      }

      const refresh$ = isAdminRequest ? adminAuth.refreshToken() : account.refreshToken();

      return refresh$.pipe(
        switchMap(refreshed => {
          if (!refreshed) {
            router.navigate([isAdminRequest ? '/admin/auth/login' : '/auth/login']);
            return throwError(() => error);
          }

          const retryReq = req.clone({ url: apiUrl, setHeaders: { Authorization: `Bearer ${refreshed.token}` } });
          return next(retryReq);
        })
      );
    })
  );
};
```

- [ ] **Step 2: Verify it compiles**

```powershell
npx tsc --noEmit -p frontend/tsconfig.app.json
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/api.interceptor.ts
git commit -m "Branch api.interceptor.ts by /Admin/ prefix to attach the correct bearer token"
```

---

## Task 15: Admin sidebar layout (matching the reference design)

**Files:**
- Modify: `frontend/src/app/admin/features/layouts/main-layout/main-layout.ts`
- Modify: `frontend/src/app/admin/features/layouts/main-layout/main-layout.html`
- Modify: `frontend/src/app/admin/features/layouts/main-layout/main-layout.scss`
- Modify: `frontend/src/app/admin/shared/scss/_variables.scss` (add cream/forest-green tokens)
- Delete: `frontend/src/app/admin/features/layouts/navbar/` (unused after this task — the reference has no top navbar in the admin area)
- Delete: `frontend/src/app/admin/features/layouts/footer/` (unused after this task — the reference has no footer in the admin area)

**Interfaces:**
- Consumes: `AdminAuthServices.user`/`.hasPermission()`/`.logout()` (Task 12).
- Produces: `AdminLayoutComponent` (selector `app-admin-layout`) — Task 18's routing wraps all `/admin/**` routes in it. Its nav item list is filtered by `hasPermission()`, driven by a `NAV_ITEMS` array — later phases (Categories/Products/Orders/Sliders/Customers) add entries here rather than duplicating the layout.

- [ ] **Step 1: Add cream/forest-green tokens**

Append to `frontend/src/app/admin/shared/scss/_variables.scss` (same hex values as the customer account area — see `frontend/src/app/site/features/layouts/account-layout/account-layout.scss` — so both admin areas of the app share one palette):

```scss
$admin-bg: #f7f4ee;
$admin-green: #2c5545;
$admin-green-hover: #234435;
$admin-text: #1a1a1a;
$admin-muted: #6b7280;
$admin-sidebar-bg: #ffffff;
$admin-radius: 14px;
```

- [ ] **Step 2: Delete the unused navbar/footer stubs**

```powershell
Remove-Item -Recurse -Force frontend/src/app/admin/features/layouts/navbar
Remove-Item -Recurse -Force frontend/src/app/admin/features/layouts/footer
```

- [ ] **Step 3: Write the layout component**

```typescript
// frontend/src/app/admin/features/layouts/main-layout/main-layout.ts
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';

interface AdminNavItem {
  label: string;
  path: string;
  icon: string;
  permission: string;
}

const NAV_ITEMS: AdminNavItem[] = [
  { label: 'Dashboard', path: '.', icon: 'bi-grid-1x2-fill', permission: 'dashboard.view' },
  { label: 'Roles', path: 'roles', icon: 'bi-shield-lock-fill', permission: 'roles.manage' },
  { label: 'Admins', path: 'admins', icon: 'bi-people-fill', permission: 'admins.manage' },
];

@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './main-layout.html',
  styleUrl: './main-layout.scss',
})
export class AdminLayoutComponent {
  private auth = inject(AdminAuthServices);
  private router = inject(Router);

  collapsed = signal(false);
  admin = this.auth.user;

  visibleNavItems = () => NAV_ITEMS.filter(item => this.auth.hasPermission(item.permission));

  toggleCollapsed(): void {
    this.collapsed.update(v => !v);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/admin/auth/login');
  }
}
```

- [ ] **Step 4: Write the template**

```html
<!-- frontend/src/app/admin/features/layouts/main-layout/main-layout.html -->
<div class="admin-shell" [class.collapsed]="collapsed()">
  <aside class="admin-sidebar">
    <div class="sidebar-header">
      <span class="brand-badge">S</span>
      @if (!collapsed()) {
        <span class="brand-label">Admin Panel</span>
      }
      <button type="button" class="collapse-btn" (click)="toggleCollapsed()">
        <i class="bi" [class.bi-chevron-left]="!collapsed()" [class.bi-chevron-right]="collapsed()"></i>
      </button>
    </div>

    <nav class="sidebar-nav">
      @for (item of visibleNavItems(); track item.path) {
        <a [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: item.path === '.' }" class="nav-link">
          <i class="bi {{ item.icon }} nav-icon"></i>
          @if (!collapsed()) {
            <span class="nav-label">{{ item.label }}</span>
          }
        </a>
      }
    </nav>

    <div class="sidebar-footer">
      @if (!collapsed() && admin()) {
        <div class="admin-card">
          <p class="admin-name">{{ admin()!.firstName }} {{ admin()!.lastName }}</p>
          <p class="admin-email">{{ admin()!.email }}</p>
        </div>
      }
      <button type="button" class="logout-btn" (click)="logout()">
        <i class="bi bi-box-arrow-right"></i>
        @if (!collapsed()) {
          <span>Log out</span>
        }
      </button>
    </div>
  </aside>

  <main class="admin-content">
    <router-outlet />
  </main>
</div>
```

- [ ] **Step 5: Write the styles**

```scss
// frontend/src/app/admin/features/layouts/main-layout/main-layout.scss
@import '../../../shared/scss/variables';

.admin-shell {
  display: flex;
  min-height: 100vh;
  background: $admin-bg;
}

.admin-sidebar {
  width: 260px;
  background: $admin-sidebar-bg;
  border-right: 1px solid rgba(0, 0, 0, 0.06);
  display: flex;
  flex-direction: column;
  padding: 1.25rem 1rem;
  transition: width 0.15s ease;

  .collapsed & {
    width: 76px;
  }
}

.sidebar-header {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin-bottom: 1.5rem;

  .brand-badge {
    width: 2rem;
    height: 2rem;
    border-radius: 50%;
    background: $admin-green;
    color: #fff;
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: 700;
  }

  .brand-label {
    font-weight: 800;
    color: $admin-text;
    flex: 1;
  }

  .collapse-btn {
    border: none;
    background: transparent;
    color: $admin-muted;
    cursor: pointer;
  }
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
  flex: 1;
}

.nav-link {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 0.9rem;
  border-radius: $admin-radius;
  color: $admin-muted;
  text-decoration: none;
  font-weight: 600;
  transition: background-color 0.15s ease, color 0.15s ease;

  &:hover {
    background: rgba($admin-green, 0.08);
    color: $admin-text;
  }

  &.active {
    background: $admin-green;
    color: #fff;

    &:hover {
      background: $admin-green-hover;
    }
  }
}

.nav-icon {
  width: 1.25rem;
  text-align: center;
}

.sidebar-footer {
  margin-top: auto;
  padding-top: 0.75rem;
  border-top: 1px solid rgba(0, 0, 0, 0.06);
}

.admin-card {
  padding: 0.6rem 0.75rem;
  border-radius: $admin-radius;
  background: $admin-bg;
  margin-bottom: 0.5rem;

  .admin-name {
    font-weight: 700;
    font-size: 0.85rem;
    color: $admin-text;
    margin: 0;
  }

  .admin-email {
    font-size: 0.75rem;
    color: $admin-muted;
    margin: 0;
  }
}

.logout-btn {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  padding: 0.75rem 0.9rem;
  border: none;
  background: transparent;
  border-radius: $admin-radius;
  color: #b3261e;
  font-weight: 600;
  cursor: pointer;

  &:hover {
    background: rgba(#b3261e, 0.08);
  }
}

.admin-content {
  flex: 1;
  padding: 2.5rem;
  min-width: 0;
}
```

- [ ] **Step 6: Run the dev server and verify visually**

```powershell
cd frontend
npm start
```

Navigate to `http://localhost:4200/admin` in a browser (Task 18 wires the route — if it 404s at this point, that's expected until Task 18; verify the component renders once Task 18 is done, or temporarily add a throwaway route to smoke-test the layout now if you want to check styling before Task 18).

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/admin/features/layouts/main-layout frontend/src/app/admin/shared/scss/_variables.scss
git rm -r frontend/src/app/admin/features/layouts/navbar frontend/src/app/admin/features/layouts/footer
git commit -m "Replace admin main-layout stub with a sidebar shell matching the reference design"
```

---

## Task 16: Admin auth shell + login page

**Files:**
- Modify: `frontend/src/app/admin/features/layouts/auth-layout/auth-layout.ts`
- Modify: `frontend/src/app/admin/features/layouts/auth-layout/auth-layout.html`
- Modify: `frontend/src/app/admin/features/layouts/auth-layout/auth-layout.scss`
- Modify: `frontend/src/app/admin/features/auth/login/login.ts`
- Modify: `frontend/src/app/admin/features/auth/login/login.html`
- Modify: `frontend/src/app/admin/features/auth/login/login.scss`

**Interfaces:**
- Consumes: `AdminAuthServices.login()` (Task 12).
- Produces: `AdminAuthLayoutComponent` (selector `app-admin-auth-layout`, a `<router-outlet>` shell) and `AdminLoginComponent` — Task 18 routes `/admin/auth/login` to it.

- [ ] **Step 1: Write the auth shell**

```typescript
// frontend/src/app/admin/features/layouts/auth-layout/auth-layout.ts
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-admin-auth-layout',
  imports: [RouterOutlet],
  templateUrl: './auth-layout.html',
  styleUrl: './auth-layout.scss',
})
export class AdminAuthLayoutComponent {}
```

```html
<!-- frontend/src/app/admin/features/layouts/auth-layout/auth-layout.html -->
<div class="admin-auth-shell">
  <div class="admin-auth-card">
    <div class="admin-auth-brand">
      <span class="brand-badge">S</span>
      <span>ShopDemo Admin</span>
    </div>
    <router-outlet />
  </div>
</div>
```

```scss
// frontend/src/app/admin/features/layouts/auth-layout/auth-layout.scss
@import '../../../shared/scss/variables';

.admin-auth-shell {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: $admin-bg;
  padding: 1.5rem;
}

.admin-auth-card {
  width: 100%;
  max-width: 400px;
  background: #fff;
  border-radius: 18px;
  padding: 2.5rem 2rem;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.06);
}

.admin-auth-brand {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-weight: 800;
  color: $admin-text;
  margin-bottom: 1.5rem;

  .brand-badge {
    width: 2rem;
    height: 2rem;
    border-radius: 50%;
    background: $admin-green;
    color: #fff;
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: 700;
  }
}
```

- [ ] **Step 2: Write the login page**

```typescript
// frontend/src/app/admin/features/auth/login/login.ts
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';

@Component({
  selector: 'app-admin-login',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginComponent {
  private auth = inject(AdminAuthServices);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private fb = inject(FormBuilder);

  submitting = signal(false);
  error = signal('');

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.submitting.set(false);
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl') ?? '/admin';
        this.router.navigateByUrl(returnUrl);
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('Invalid email or password.');
      },
    });
  }
}
```

```html
<!-- frontend/src/app/admin/features/auth/login/login.html -->
<h2 class="form-title">Sign in</h2>

@if (error()) {
  <div class="alert-error">{{ error() }}</div>
}

<form [formGroup]="form" (ngSubmit)="submit()" class="admin-auth-form">
  <div class="field-group">
    <label>Email</label>
    <input formControlName="email" type="email" class="form-control" placeholder="you@example.com">
  </div>
  <div class="field-group">
    <label>Password</label>
    <input formControlName="password" type="password" class="form-control" placeholder="********">
  </div>

  <button type="submit" class="submit-btn" [disabled]="submitting()">{{ submitting() ? 'Signing in…' : 'Sign In' }}</button>
</form>

<p class="forgot-link">
  <a routerLink="/admin/auth/forgot-password">Forgot your password?</a>
</p>
```

```scss
// frontend/src/app/admin/features/auth/login/login.scss
@import '../../../shared/scss/variables';

.form-title {
  font-weight: 800;
  margin-bottom: 1rem;
  color: $admin-text;
}

.alert-error {
  background: rgba(#b3261e, 0.08);
  color: #b3261e;
  padding: 0.6rem 0.75rem;
  border-radius: 10px;
  font-size: 0.85rem;
  margin-bottom: 1rem;
}

.admin-auth-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.field-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;

  label {
    font-weight: 600;
    font-size: 0.85rem;
    color: $admin-text;
  }
}

.form-control {
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.6rem 0.75rem;
}

.submit-btn {
  background: $admin-green;
  color: #fff;
  border: none;
  border-radius: 10px;
  padding: 0.75rem;
  font-weight: 700;
  cursor: pointer;

  &:hover {
    background: $admin-green-hover;
  }

  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }
}

.forgot-link {
  margin-top: 1rem;
  text-align: center;
  font-size: 0.85rem;

  a {
    color: $admin-green;
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/admin/features/layouts/auth-layout frontend/src/app/admin/features/auth/login
git commit -m "Build out admin auth shell and login page"
```

(Manual browser verification happens together with Task 18, once routing exists.)

---

## Task 17: Admin forgot-password / reset-password pages

**Files:**
- Create: `frontend/src/app/admin/features/auth/forgot-password/forgot-password.ts`
- Create: `frontend/src/app/admin/features/auth/forgot-password/forgot-password.html`
- Create: `frontend/src/app/admin/features/auth/forgot-password/forgot-password.scss`
- Create: `frontend/src/app/admin/features/auth/reset-password/reset-password.ts`
- Create: `frontend/src/app/admin/features/auth/reset-password/reset-password.html`
- Create: `frontend/src/app/admin/features/auth/reset-password/reset-password.scss`

**Interfaces:**
- Consumes: `AdminAuthServices.forgotPassword()`/`.resetPassword()` (Task 12).
- Produces: `ForgotPasswordComponent`, `ResetPasswordComponent` — Task 18 routes `/admin/auth/forgot-password` and `/admin/auth/reset-password` to them. `ResetPasswordComponent` reads `email`/`token` from query params (matching the link `AdminAuthService.ForgotPasswordAsync` emails — Task 9).

- [ ] **Step 1: Write the forgot-password page**

```typescript
// frontend/src/app/admin/features/auth/forgot-password/forgot-password.ts
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';

@Component({
  selector: 'app-admin-forgot-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.scss',
})
export class ForgotPasswordComponent {
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  submitting = signal(false);
  submitted = signal(false);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.auth.forgotPassword(this.form.getRawValue().email).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitted.set(true);
      },
      error: () => {
        this.submitting.set(false);
        this.submitted.set(true); // same message either way — don't reveal whether the email exists
      },
    });
  }
}
```

```html
<!-- frontend/src/app/admin/features/auth/forgot-password/forgot-password.html -->
<h2 class="form-title">Forgot password</h2>

@if (submitted()) {
  <p class="state-message">If that email is registered, a reset link has been sent. Check your inbox.</p>
} @else {
  <form [formGroup]="form" (ngSubmit)="submit()" class="admin-auth-form">
    <div class="field-group">
      <label>Email</label>
      <input formControlName="email" type="email" class="form-control" placeholder="you@example.com">
    </div>

    <button type="submit" class="submit-btn" [disabled]="submitting()">{{ submitting() ? 'Sending…' : 'Send reset link' }}</button>
  </form>
}

<p class="forgot-link">
  <a routerLink="/admin/auth/login">Back to sign in</a>
</p>
```

```scss
// frontend/src/app/admin/features/auth/forgot-password/forgot-password.scss
@import '../../../shared/scss/variables';

.form-title {
  font-weight: 800;
  margin-bottom: 1rem;
  color: $admin-text;
}

.state-message {
  color: $admin-muted;
  font-size: 0.9rem;
}

.admin-auth-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.field-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;

  label {
    font-weight: 600;
    font-size: 0.85rem;
    color: $admin-text;
  }
}

.form-control {
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.6rem 0.75rem;
}

.submit-btn {
  background: $admin-green;
  color: #fff;
  border: none;
  border-radius: 10px;
  padding: 0.75rem;
  font-weight: 700;
  cursor: pointer;

  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }
}

.forgot-link {
  margin-top: 1rem;
  text-align: center;
  font-size: 0.85rem;

  a {
    color: $admin-green;
  }
}
```

- [ ] **Step 2: Write the reset-password page**

```typescript
// frontend/src/app/admin/features/auth/reset-password/reset-password.ts
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';

@Component({
  selector: 'app-admin-reset-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss',
})
export class ResetPasswordComponent {
  private auth = inject(AdminAuthServices);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private fb = inject(FormBuilder);

  submitting = signal(false);
  done = signal(false);
  error = signal('');

  private email = this.route.snapshot.queryParamMap.get('email') ?? '';
  private token = this.route.snapshot.queryParamMap.get('token') ?? '';

  form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid || !this.email || !this.token) {
      this.form.markAllAsTouched();
      this.error.set(!this.email || !this.token ? 'This reset link is missing required information.' : '');
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    this.auth.resetPassword({ email: this.email, token: this.token, newPassword: this.form.getRawValue().newPassword }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.done.set(true);
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('This reset link is invalid or has expired. Request a new one.');
      },
    });
  }
}
```

```html
<!-- frontend/src/app/admin/features/auth/reset-password/reset-password.html -->
<h2 class="form-title">Set a new password</h2>

@if (error()) {
  <div class="alert-error">{{ error() }}</div>
}

@if (done()) {
  <p class="state-message">Your password has been updated.</p>
  <p class="forgot-link"><a routerLink="/admin/auth/login">Sign in</a></p>
} @else {
  <form [formGroup]="form" (ngSubmit)="submit()" class="admin-auth-form">
    <div class="field-group">
      <label>New password</label>
      <input formControlName="newPassword" type="password" class="form-control" placeholder="At least 8 characters">
    </div>

    <button type="submit" class="submit-btn" [disabled]="submitting()">{{ submitting() ? 'Saving…' : 'Set password' }}</button>
  </form>
}
```

```scss
// frontend/src/app/admin/features/auth/reset-password/reset-password.scss
@import '../../../shared/scss/variables';

.form-title {
  font-weight: 800;
  margin-bottom: 1rem;
  color: $admin-text;
}

.alert-error {
  background: rgba(#b3261e, 0.08);
  color: #b3261e;
  padding: 0.6rem 0.75rem;
  border-radius: 10px;
  font-size: 0.85rem;
  margin-bottom: 1rem;
}

.state-message {
  color: $admin-muted;
  font-size: 0.9rem;
}

.admin-auth-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.field-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;

  label {
    font-weight: 600;
    font-size: 0.85rem;
    color: $admin-text;
  }
}

.form-control {
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.6rem 0.75rem;
}

.submit-btn {
  background: $admin-green;
  color: #fff;
  border: none;
  border-radius: 10px;
  padding: 0.75rem;
  font-weight: 700;
  cursor: pointer;

  &:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }
}

.forgot-link {
  margin-top: 1rem;
  text-align: center;
  font-size: 0.85rem;

  a {
    color: $admin-green;
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/app/admin/features/auth/forgot-password frontend/src/app/admin/features/auth/reset-password
git commit -m "Add admin forgot-password and reset-password pages"
```

(Manual browser verification happens together with Task 18, once routing exists.)

---

## Task 18: Wire up admin routing + a placeholder Dashboard page

**Files:**
- Create: `frontend/src/app/admin/features/pages/dashboard/dashboard.ts`
- Create: `frontend/src/app/admin/features/pages/dashboard/dashboard.html`
- Create: `frontend/src/app/admin/features/pages/dashboard/dashboard.scss`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.routes.server.ts`

**Interfaces:**
- Consumes: `AdminLayoutComponent` (Task 15), `AdminAuthLayoutComponent`/`LoginComponent`/`ForgotPasswordComponent`/`ResetPasswordComponent` (Tasks 16–17), `adminAuthGuard`/`adminPermissionGuard` (Task 13).
- Produces: the actual `/admin/**` route tree every earlier frontend task has been assuming exists. `DashboardComponent` is an intentionally minimal placeholder — Phase 5 replaces its contents with the real overview (summary cards, recent orders, low-stock table); its route and nav entry already exist so this task doesn't need revisiting then.

- [ ] **Step 1: Write the placeholder Dashboard page**

```typescript
// frontend/src/app/admin/features/pages/dashboard/dashboard.ts
import { Component } from '@angular/core';

@Component({
  selector: 'app-admin-dashboard',
  imports: [],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardComponent {}
```

```html
<!-- frontend/src/app/admin/features/pages/dashboard/dashboard.html -->
<h1 class="page-title">Dashboard</h1>
<p class="page-subtitle">Welcome back! The full overview (sales, orders, stock alerts) ships in a later phase.</p>
```

```scss
// frontend/src/app/admin/features/pages/dashboard/dashboard.scss
@import '../../../shared/scss/variables';

.page-title {
  font-weight: 800;
  color: $admin-text;
}

.page-subtitle {
  color: $admin-muted;
}
```

- [ ] **Step 2: Wire `app.routes.ts`**

Add these imports near the top of `frontend/src/app/app.routes.ts`, alongside the existing site imports:

```typescript
import { AdminLayoutComponent } from './admin/features/layouts/main-layout/main-layout';
import { AdminAuthLayoutComponent } from './admin/features/layouts/auth-layout/auth-layout';
import { LoginComponent as AdminLoginComponent } from './admin/features/auth/login/login';
import { ForgotPasswordComponent as AdminForgotPasswordComponent } from './admin/features/auth/forgot-password/forgot-password';
import { ResetPasswordComponent as AdminResetPasswordComponent } from './admin/features/auth/reset-password/reset-password';
import { DashboardComponent } from './admin/features/pages/dashboard/dashboard';
import { Admins as AdminsComponent } from './admin/features/pages/admins/admins';
import { RolesComponent } from './admin/features/pages/roles/roles';
import { adminAuthGuard } from './admin/core/guards/admin-auth-guard';
import { adminPermissionGuard } from './admin/core/guards/admin-permission-guard';
```

(`RolesComponent` doesn't exist until Task 19 and the real `Admins` page content doesn't exist until Task 20 — this task's routes will fail to compile until both land. Do Tasks 19–20 in the same working session as this one, or comment out the two imports/routes temporarily and finish them here once those tasks are done.)

Add a new top-level route array entry (a sibling of the existing `{ path: 'auth', ... }` and `{ path: '', component: MainLayoutComponent, ... }` entries — order matters: put it before the trailing `{ path: '**', ... }` wildcard):

```typescript
    { path: 'admin/auth', component: AdminAuthLayoutComponent, title: 'Admin', children: [
        { path: 'login', component: AdminLoginComponent, title: 'Admin Login' },
        { path: 'forgot-password', component: AdminForgotPasswordComponent, title: 'Forgot Password' },
        { path: 'reset-password', component: AdminResetPasswordComponent, title: 'Reset Password' },
    ]},
    { path: 'admin', component: AdminLayoutComponent, canActivate: [adminAuthGuard], children: [
        { path: '', component: DashboardComponent, title: 'Admin Dashboard' },
        { path: 'roles', component: RolesComponent, canActivate: [adminPermissionGuard('roles.manage')], title: 'Roles' },
        { path: 'admins', component: AdminsComponent, canActivate: [adminPermissionGuard('admins.manage')], title: 'Admins' },
    ]},
```

- [ ] **Step 3: Wire `app.routes.server.ts`**

The whole admin area reads from `localStorage` (admin session) and always calls the backend — none of it can be prerendered. Add to `frontend/src/app/app.routes.server.ts`, before the trailing `{ path: '**', renderMode: RenderMode.Prerender }` entry:

```typescript
  {
    path: 'admin/auth/login',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin/auth/forgot-password',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin/auth/reset-password',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin/roles',
    renderMode: RenderMode.Client
  },
  {
    path: 'admin/admins',
    renderMode: RenderMode.Client
  },
```

- [ ] **Step 4: Manually verify in the browser**

Backend must be running (`dotnet run --project backend/Ecommerce`) with the Task 3 seed data present.

```powershell
cd frontend
npm start
```

- Navigate to `http://localhost:4200/admin` while logged out (clear `localStorage` first if a previous manual test left a session). Expected: redirected to `http://localhost:4200/admin/auth/login?returnUrl=%2Fadmin`.
- Log in with `admin.tester@example.com` / `AdminTester@123`. Expected: redirected to `/admin`, sidebar shows Dashboard/Roles/Admins (Super Admin has every permission), profile card shows "Admin Tester".
- Click "Log out". Expected: redirected to `/admin/auth/login`, and navigating back to `/admin` redirects to login again (session cleared).
- With dev tools open, confirm `localStorage` has a `shopdemo_admin_auth` key while logged in and that the customer-facing `/account` area (if you're also logged in there) still uses `shopdemo_auth` independently — log into both in the same browser and confirm neither logout affects the other.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/app/admin/features/pages/dashboard frontend/src/app/app.routes.ts frontend/src/app/app.routes.server.ts
git commit -m "Wire up /admin/** routing (auth pages, guarded shell, placeholder dashboard)"
```

---

## Task 19: Roles & Permissions page

**Files:**
- Create: `frontend/src/app/admin/shared/interface/role-interfaces.ts`
- Create: `frontend/src/app/admin/core/services/role-services.ts`
- Create: `frontend/src/app/admin/features/pages/roles/roles.ts`
- Create: `frontend/src/app/admin/features/pages/roles/roles.html`
- Create: `frontend/src/app/admin/features/pages/roles/roles.scss`

**Interfaces:**
- Consumes: `GET/POST/PUT/DELETE api/Admin/Roles`, `GET api/Admin/Permissions` (Task 10).
- Produces: `RolesComponent` — imported and routed by Task 18 (that task's `app.routes.ts` edit already references it by this exact name).

- [ ] **Step 1: Write the interfaces**

```typescript
// frontend/src/app/admin/shared/interface/role-interfaces.ts
export interface PermissionInterface {
  id: number;
  key: string;
  module: string;
  description: string;
}

export interface RoleInterface {
  id: number;
  name: string;
  description?: string;
  isSystem: boolean;
  permissions: PermissionInterface[];
}

export interface RoleRequest {
  name: string;
  description?: string;
  permissionKeys: string[];
}
```

- [ ] **Step 2: Write `RoleServices`**

```typescript
// frontend/src/app/admin/core/services/role-services.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { PermissionInterface, RoleInterface, RoleRequest } from '../../shared/interface/role-interfaces';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class RoleServices {
  private http = inject(HttpClient);

  getRoles(): Observable<RoleInterface[]> {
    return this.http.get<AdminApiEnvelope<RoleInterface[]>>('/Admin/Roles').pipe(map(response => response.data));
  }

  getPermissionCatalog(): Observable<PermissionInterface[]> {
    return this.http.get<AdminApiEnvelope<PermissionInterface[]>>('/Admin/Permissions').pipe(map(response => response.data));
  }

  createRole(request: RoleRequest): Observable<RoleInterface> {
    return this.http.post<AdminApiEnvelope<RoleInterface>>('/Admin/Roles', request).pipe(map(response => response.data));
  }

  updateRole(id: number, request: RoleRequest): Observable<RoleInterface> {
    return this.http.put<AdminApiEnvelope<RoleInterface>>(`/Admin/Roles/${id}`, request).pipe(map(response => response.data));
  }

  deleteRole(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Roles/${id}`).pipe(map(() => undefined));
  }
}
```

- [ ] **Step 3: Write the component**

```typescript
// frontend/src/app/admin/features/pages/roles/roles.ts
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RoleServices } from '../../../core/services/role-services';
import { PermissionInterface, RoleInterface } from '../../../shared/interface/role-interfaces';

@Component({
  selector: 'app-roles',
  imports: [ReactiveFormsModule],
  templateUrl: './roles.html',
  styleUrl: './roles.scss',
})
export class RolesComponent {
  private roleService = inject(RoleServices);
  private fb = inject(FormBuilder);

  roles = signal<RoleInterface[]>([]);
  permissionCatalog = signal<PermissionInterface[]>([]);
  selectedPermissionKeys = signal<Set<string>>(new Set());

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  permissionsByModule = computed(() => {
    const groups = new Map<string, PermissionInterface[]>();
    for (const permission of this.permissionCatalog()) {
      const group = groups.get(permission.module) ?? [];
      group.push(permission);
      groups.set(permission.module, group);
    }
    return Array.from(groups.entries()).map(([module, permissions]) => ({ module, permissions }));
  });

  form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    description: [''],
  });

  constructor() {
    this.roleService.getPermissionCatalog().subscribe(catalog => this.permissionCatalog.set(catalog));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.roleService.getRoles().subscribe({
      next: data => {
        this.roles.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startAdd(): void {
    this.editingId.set(null);
    this.form.reset();
    this.selectedPermissionKeys.set(new Set());
    this.showForm.set(true);
  }

  startEdit(role: RoleInterface): void {
    this.editingId.set(role.id);
    this.form.reset({ name: role.name, description: role.description ?? '' });
    this.selectedPermissionKeys.set(new Set(role.permissions.map(p => p.key)));
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  isChecked(key: string): boolean {
    return this.selectedPermissionKeys().has(key);
  }

  togglePermission(key: string, checked: boolean): void {
    this.selectedPermissionKeys.update(keys => {
      const next = new Set(keys);
      checked ? next.add(key) : next.delete(key);
      return next;
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();
    const request = {
      name: raw.name,
      description: raw.description || undefined,
      permissionKeys: Array.from(this.selectedPermissionKeys()),
    };

    const editingId = this.editingId();
    const request$ = editingId ? this.roleService.updateRole(editingId, request) : this.roleService.createRole(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this role. Check the name is unique and try again.');
      },
    });
  }

  remove(role: RoleInterface): void {
    this.busyId.set(role.id);
    this.roleService.deleteRole(role.id).subscribe({
      next: () => {
        this.roles.update(items => items.filter(r => r.id !== role.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
```

- [ ] **Step 4: Write the template**

```html
<!-- frontend/src/app/admin/features/pages/roles/roles.html -->
<div class="panel-header">
  <div>
    <h1 class="page-title">Roles &amp; Permissions</h1>
    <p class="page-subtitle">Define roles and toggle exactly what each one can do.</p>
  </div>
  @if (!showForm()) {
    <button type="button" class="add-btn" (click)="startAdd()">+ Add Role</button>
  }
</div>

@if (loading()) {
  <div class="state-message">Loading roles…</div>
} @else if (!showForm()) {
  <table class="data-table">
    <thead>
      <tr>
        <th>Name</th>
        <th>Description</th>
        <th>Permissions</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      @for (role of roles(); track role.id) {
        <tr>
          <td>
            {{ role.name }}
            @if (role.isSystem) {
              <span class="system-badge">System</span>
            }
          </td>
          <td>{{ role.description || '—' }}</td>
          <td>{{ role.permissions.length }}</td>
          <td class="actions">
            <button type="button" (click)="startEdit(role)" [disabled]="role.isSystem">Edit</button>
            <button type="button" class="danger" [disabled]="role.isSystem || busyId() === role.id" (click)="remove(role)">Delete</button>
          </td>
        </tr>
      }
    </tbody>
  </table>
}

@if (showForm()) {
  @if (error()) {
    <div class="alert-error">{{ error() }}</div>
  }

  <form [formGroup]="form" (ngSubmit)="save()" class="role-form">
    <div class="field-group">
      <label>Name</label>
      <input formControlName="name" type="text" class="form-control">
    </div>
    <div class="field-group">
      <label>Description</label>
      <input formControlName="description" type="text" class="form-control">
    </div>

    <div class="permission-groups">
      @for (group of permissionsByModule(); track group.module) {
        <div class="permission-group">
          <h4>{{ group.module }}</h4>
          @for (permission of group.permissions; track permission.id) {
            <label class="checkbox-field">
              <input
                type="checkbox"
                [checked]="isChecked(permission.key)"
                (change)="togglePermission(permission.key, $any($event.target).checked)">
              {{ permission.description }}
            </label>
          }
        </div>
      }
    </div>

    <div class="form-actions">
      <button type="submit" class="save-btn" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save Role' }}</button>
      <button type="button" class="cancel-btn" (click)="cancel()">Cancel</button>
    </div>
  </form>
}
```

- [ ] **Step 5: Write the styles**

```scss
// frontend/src/app/admin/features/pages/roles/roles.scss
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
  }

  th {
    color: $admin-muted;
    font-size: 0.8rem;
    text-transform: uppercase;
  }
}

.system-badge {
  margin-left: 0.5rem;
  background: rgba($admin-green, 0.12);
  color: $admin-green;
  padding: 0.15rem 0.5rem;
  border-radius: 999px;
  font-size: 0.7rem;
  font-weight: 700;
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

.alert-error {
  background: rgba(#b3261e, 0.08);
  color: #b3261e;
  padding: 0.6rem 0.75rem;
  border-radius: 10px;
  font-size: 0.85rem;
  margin-bottom: 1rem;
}

.role-form {
  background: #fff;
  border-radius: $admin-radius;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 640px;
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

.permission-groups {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.permission-group {
  h4 {
    font-size: 0.85rem;
    font-weight: 700;
    color: $admin-text;
    margin-bottom: 0.5rem;
  }
}

.checkbox-field {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  margin-bottom: 0.35rem;
}

.form-actions {
  display: flex;
  gap: 0.75rem;
}

.cancel-btn {
  background: transparent;
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.65rem 1.1rem;
  font-weight: 600;
  cursor: pointer;
}
```

- [ ] **Step 6: Manually verify**

With the frontend and backend running and logged in as `admin.tester@example.com`, go to `/admin/roles`. Confirm the Super Admin role shows as "System" (Edit/Delete disabled), click "+ Add Role", create a role named "Support" with a couple of permissions checked, confirm it appears in the list with the right permission count, edit it to toggle a permission off, then delete it.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/admin/shared/interface/role-interfaces.ts frontend/src/app/admin/core/services/role-services.ts frontend/src/app/admin/features/pages/roles
git commit -m "Add Roles & Permissions page"
```

---

## Task 20: Admins page

**Files:**
- Create: `frontend/src/app/admin/shared/interface/admin-user-interfaces.ts`
- Create: `frontend/src/app/admin/core/services/admin-user-services.ts`
- Modify: `frontend/src/app/admin/features/pages/admins/admins.ts` (currently an empty stub)
- Modify: `frontend/src/app/admin/features/pages/admins/admins.html`
- Modify: `frontend/src/app/admin/features/pages/admins/admins.scss`

**Interfaces:**
- Consumes: `GET/POST/PUT/DELETE api/Admin/Admins`, `PUT api/Admin/Admins/{id}/status` (Task 11), `RoleServices.getRoles()` (Task 19, for the role picker).
- Produces: `Admins` component (the existing class name in the stub — Task 18's routing already imports it as `Admins as AdminsComponent`, so keep this exact class name).

- [ ] **Step 1: Write the interfaces**

```typescript
// frontend/src/app/admin/shared/interface/admin-user-interfaces.ts
export interface AdminUserInterface {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
  roleId: number;
  roleName: string;
  isActive: boolean;
  createdOn: string;
}

export interface CreateAdminUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
  roleId: number;
}

export interface UpdateAdminUserRequest {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  roleId: number;
  isActive: boolean;
}
```

- [ ] **Step 2: Write `AdminUserServices`**

```typescript
// frontend/src/app/admin/core/services/admin-user-services.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { AdminUserInterface, CreateAdminUserRequest, UpdateAdminUserRequest } from '../../shared/interface/admin-user-interfaces';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class AdminUserServices {
  private http = inject(HttpClient);

  getAdmins(): Observable<AdminUserInterface[]> {
    return this.http.get<AdminApiEnvelope<AdminUserInterface[]>>('/Admin/Admins').pipe(map(response => response.data));
  }

  createAdmin(request: CreateAdminUserRequest): Observable<AdminUserInterface> {
    return this.http.post<AdminApiEnvelope<AdminUserInterface>>('/Admin/Admins', request).pipe(map(response => response.data));
  }

  updateAdmin(id: number, request: UpdateAdminUserRequest): Observable<AdminUserInterface> {
    return this.http.put<AdminApiEnvelope<AdminUserInterface>>(`/Admin/Admins/${id}`, request).pipe(map(response => response.data));
  }

  setAdminStatus(id: number, isActive: boolean): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Admins/${id}/status`, { isActive }).pipe(map(() => undefined));
  }

  deleteAdmin(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Admins/${id}`).pipe(map(() => undefined));
  }
}
```

- [ ] **Step 3: Write the component**

```typescript
// frontend/src/app/admin/features/pages/admins/admins.ts
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminUserServices } from '../../../core/services/admin-user-services';
import { RoleServices } from '../../../core/services/role-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { AdminUserInterface } from '../../../shared/interface/admin-user-interfaces';
import { RoleInterface } from '../../../shared/interface/role-interfaces';

@Component({
  selector: 'app-admins',
  imports: [ReactiveFormsModule],
  templateUrl: './admins.html',
  styleUrl: './admins.scss',
})
export class Admins {
  private adminUserService = inject(AdminUserServices);
  private roleService = inject(RoleServices);
  private auth = inject(AdminAuthServices);

  private fb = inject(FormBuilder);

  admins = signal<AdminUserInterface[]>([]);
  roles = signal<RoleInterface[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  currentAdminId = () => this.auth.user()?.id;

  form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: [''],
    roleId: [0, Validators.required],
  });

  constructor() {
    this.roleService.getRoles().subscribe(roles => this.roles.set(roles));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.adminUserService.getAdmins().subscribe({
      next: data => {
        this.admins.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startAdd(): void {
    this.editingId.set(null);
    this.form.reset({ roleId: this.roles()[0]?.id ?? 0 });
    this.form.get('email')?.enable();
    this.showForm.set(true);
  }

  startEdit(admin: AdminUserInterface): void {
    this.editingId.set(admin.id);
    this.form.reset({
      firstName: admin.firstName,
      lastName: admin.lastName,
      email: admin.email,
      phoneNumber: admin.phoneNumber ?? '',
      roleId: admin.roleId,
    });
    this.form.get('email')?.disable(); // email is immutable after creation
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();
    const editingId = this.editingId();

    const request$ = editingId
      ? this.adminUserService.updateAdmin(editingId, {
          firstName: raw.firstName,
          lastName: raw.lastName,
          phoneNumber: raw.phoneNumber || undefined,
          roleId: raw.roleId,
          isActive: this.admins().find(a => a.id === editingId)?.isActive ?? true,
        })
      : this.adminUserService.createAdmin({
          firstName: raw.firstName,
          lastName: raw.lastName,
          email: raw.email,
          phoneNumber: raw.phoneNumber || undefined,
          roleId: raw.roleId,
        });

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this admin. Check the email is unique and try again.');
      },
    });
  }

  toggleStatus(admin: AdminUserInterface): void {
    this.busyId.set(admin.id);
    this.adminUserService.setAdminStatus(admin.id, !admin.isActive).subscribe({
      next: () => {
        this.load();
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(admin: AdminUserInterface): void {
    this.busyId.set(admin.id);
    this.adminUserService.deleteAdmin(admin.id).subscribe({
      next: () => {
        this.admins.update(items => items.filter(a => a.id !== admin.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
```

- [ ] **Step 4: Write the template**

```html
<!-- frontend/src/app/admin/features/pages/admins/admins.html -->
<div class="panel-header">
  <div>
    <h1 class="page-title">Admins</h1>
    <p class="page-subtitle">Manage admin users and assign roles.</p>
  </div>
  @if (!showForm()) {
    <button type="button" class="add-btn" (click)="startAdd()">+ Add Admin</button>
  }
</div>

@if (loading()) {
  <div class="state-message">Loading admins…</div>
} @else if (!showForm()) {
  <table class="data-table">
    <thead>
      <tr>
        <th>Admin</th>
        <th>Contact</th>
        <th>Role</th>
        <th>Status</th>
        <th>Actions</th>
      </tr>
    </thead>
    <tbody>
      @for (admin of admins(); track admin.id) {
        <tr>
          <td>
            <span class="avatar">{{ admin.firstName.charAt(0) }}</span>
            {{ admin.firstName }} {{ admin.lastName }}
          </td>
          <td>
            {{ admin.email }}
            @if (admin.phoneNumber) {
              <div class="muted">{{ admin.phoneNumber }}</div>
            }
          </td>
          <td><span class="role-badge">{{ admin.roleName }}</span></td>
          <td>
            <label class="status-toggle">
              <input
                type="checkbox"
                [checked]="admin.isActive"
                [disabled]="admin.id === currentAdminId() || busyId() === admin.id"
                (change)="toggleStatus(admin)">
              {{ admin.isActive ? 'Active' : 'Inactive' }}
            </label>
          </td>
          <td class="actions">
            <button type="button" (click)="startEdit(admin)">Edit</button>
            <button type="button" class="danger" [disabled]="admin.id === currentAdminId() || busyId() === admin.id" (click)="remove(admin)">Delete</button>
          </td>
        </tr>
      }
    </tbody>
  </table>
}

@if (showForm()) {
  @if (error()) {
    <div class="alert-error">{{ error() }}</div>
  }

  <form [formGroup]="form" (ngSubmit)="save()" class="admin-form">
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
      @if (editingId()) {
        <small class="muted">Email can't be changed after an admin is created.</small>
      }
    </div>
    <div class="field-group">
      <label>Phone</label>
      <input formControlName="phoneNumber" type="tel" class="form-control">
    </div>
    <div class="field-group">
      <label>Role</label>
      <select formControlName="roleId" class="form-control">
        @for (role of roles(); track role.id) {
          <option [value]="role.id">{{ role.name }}</option>
        }
      </select>
    </div>

    @if (!editingId()) {
      <p class="muted">No password field — the new admin will get an email with a link to set their own password.</p>
    }

    <div class="form-actions">
      <button type="submit" class="save-btn" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save Admin' }}</button>
      <button type="button" class="cancel-btn" (click)="cancel()">Cancel</button>
    </div>
  </form>
}
```

- [ ] **Step 5: Write the styles**

```scss
// frontend/src/app/admin/features/pages/admins/admins.scss
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

.role-badge {
  background: rgba($admin-green, 0.12);
  color: $admin-green;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 700;
}

.status-toggle {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  font-size: 0.8rem;
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

.alert-error {
  background: rgba(#b3261e, 0.08);
  color: #b3261e;
  padding: 0.6rem 0.75rem;
  border-radius: 10px;
  font-size: 0.85rem;
  margin-bottom: 1rem;
}

.admin-form {
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

.cancel-btn {
  background: transparent;
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 10px;
  padding: 0.65rem 1.1rem;
  font-weight: 600;
  cursor: pointer;
}
```

- [ ] **Step 6: Manually verify**

Logged in as `admin.tester@example.com`, go to `/admin/admins`. Confirm the seeded Super Admin appears with its Edit/Delete/Status controls **disabled** on its own row (self-protection). Click "+ Add Admin", create a second admin, confirm a "set your password" email arrives in Mailtrap, complete that flow, and confirm the new admin can log in and sees a sidebar filtered to only what their assigned role permits (create the second admin with a lower-permission role, e.g. the "Support" role from Task 19's manual check, to see this in action).

- [ ] **Step 7: Commit**

```bash
git add frontend/src/app/admin/shared/interface/admin-user-interfaces.ts frontend/src/app/admin/core/services/admin-user-services.ts frontend/src/app/admin/features/pages/admins
git commit -m "Add Admins page"
```

---

## Plan-level final check

Once all 20 tasks are done:

- [ ] `dotnet test backend/Ecommerce.Tests/Ecommerce.Tests.csproj` — all passing.
- [ ] `dotnet build backend/Ecommerce.slnx` — 0 errors.
- [ ] `npx tsc --noEmit -p frontend/tsconfig.app.json` — 0 errors.
- [ ] Full manual walkthrough: admin login → dashboard → create a role with a subset of permissions → create a second admin with that role → log in as that admin → confirm their sidebar only shows what their role permits → log back in as Super Admin → deactivate the second admin → confirm that admin can no longer log in (`AdminAuth.AccountInactive`) → delete the second admin.
- [ ] Confirm the customer-facing site (`/home`, `/account`, checkout) still works unchanged — the shared `api.interceptor.ts` edit in Task 14 is the one file this plan touches that customer flows also depend on.
