# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A single-project ASP.NET Core **.NET 10** Web API for an e-commerce backend — categories, products, JWT auth, cart, orders, favorites, addresses, and saved cards. The solution is `Ecommerce.slnx`; all code lives in the `Ecommerce/` project directory. This directory is the `backend/` subtree of the `D:\ECom` monorepo (see the root `CLAUDE.md`); it was formerly its own repo, so paths in older notes referencing a standalone checkout no longer apply.

## Commands

Run all commands from this directory (`backend/`, i.e. `D:\ECom\backend`).

```powershell
dotnet build Ecommerce.slnx
dotnet run --project Ecommerce            # launches API; Scalar API docs at /scalar/v1 in Development
```

EF Core migrations (run from inside the `Ecommerce/` project dir, or pass `--project Ecommerce`):

```powershell
dotnet ef migrations add <Name> --project Ecommerce
dotnet ef database update --project Ecommerce
```

There is **no test project** in this repo yet.

### Required configuration

- **Database**: SQL Server connection string `DefaultConnection` (see `appsettings.json`; defaults to a local `.\MSSQLSERVER02` instance). EF Core migrations expect a reachable server.
- **JWT**: `Jwt:Key` is intentionally empty in `appsettings.json` — it is supplied via **user secrets** (`UserSecretsId` is set in the `.csproj`). `Jwt` options are validated on startup (`ValidateOnStart`), so a missing/short key fails fast at boot.

## Architecture

Layered-by-folder within one project. Namespaces map to folders; most common namespaces are exposed via `GlobalUsings.cs`, so new files often don't need explicit `using`s for `Ecommerce.*`, EF Core, MVC, Mapster, or FluentValidation.

**Request flow:** `Controllers/` → `Services/` (interface + impl, e.g. `ICategoryService`/`CategoryService`) → `ApplicationDbContext`. Controllers are thin; all business logic lives in services. Services are registered `Scoped` in `DependacyInjection.cs` (note the file's spelling); `AddDependancies(configuration)` in `Program.cs` wires up everything.

**Result pattern (`Abstractions/`):** Services return `Result` / `Result<T>` instead of throwing for expected failures. A failure carries an `Error(Code, Description)` (defined per-domain in `Errors/`, e.g. `CategoryErrors`, `ProductErrors`, `UserErrors`). Controllers branch on `result.IsSuccess` / `result.IsFailure`. `ResultExtensions.ToProblem(statusCode)` converts a failed result into an RFC-7807 `ProblemDetails`. When adding a new failure case, add an `Error` to the relevant `*Errors` class rather than inlining strings.

**Response envelope:** `Contracts/Common/ApiResponse<T>` wraps `{ StatusCode, Message, Data }`. Usage is inconsistent on the older `CategoriesController`/`ProductsController` — some actions return `ApiResponse<T>`, others the raw `Result`. The newer account-feature controllers (`AddressesController`, `CardsController`, `CartController`, `FavoritesController`, `OrdersController`, `ProfileController`) consistently return `ApiResponse<T>` — follow their style for new endpoints, and match the surrounding action's style when editing an existing controller.

**Contracts (`Contracts/`):** DTOs are `record`s grouped by feature (`Categories/`, `Products/`, `Authentication/`). Each request that needs validation has a sibling FluentValidation validator (e.g. `CategoryRequestValidation`) auto-discovered from the assembly and run automatically via `SharpGrip.FluentValidation.AutoValidation` — no manual `ModelState` checks needed.

**Mapping:** Entity↔DTO mapping uses **Mapster** `.Adapt<T>()`. Global config is scanned from the assembly (`AddMapsterConf`); custom rules go in an `IRegister` implementation (`Mapping/MappingConfigrations.cs`, currently a stub).

**Persistence (`Presistence/` — note spelling):** `ApplicationDbContext` extends `IdentityDbContext<ApplicationUser>`. Two important global behaviors in `OnModelCreating`/`SaveChangesAsync`:
- All cascade-delete foreign keys are rewritten to `DeleteBehavior.Restrict`.
- Entities inheriting `AuditableEntity` get `CreatedById`/`UpdatedById`/`UpdatedOn` auto-populated from the current user (via `IHttpContextAccessor`) on save.

Entity `IEntityTypeConfiguration<T>` classes live in `Presistence/EntitiesConfigurations/` and are applied via `ApplyConfigurationsFromAssembly`.

**Auth (`Authentication/` + `Services/AuthService.cs`):** ASP.NET Core Identity + JWT bearer. `IJwtProvider`/`JwtProvider` generate and validate tokens; `AuthService` handles register/login/refresh with rotating refresh tokens stored on `ApplicationUser.RefreshTokens`. The account-feature controllers (`Addresses`, `Cards`, `Cart`, `Favorites`, `Orders`, `Profile`) are all `[Authorize]` and resolve the current user's ID from `ClaimTypes.NameIdentifier` via `IHttpContextAccessor` rather than taking it as a route/body parameter. `CartController` maps service errors to HTTP statuses through a `MapFailure(Error)` helper instead of inline `NotFound`/`BadRequest` calls — a variant worth following if you add more actions there.

**Dev seed data:** `Presistence/DataSeeder.cs` (`DataSeeder.SeedAsync`), invoked from `Program.cs` only when `app.Environment.IsDevelopment()`. Seeds a fixed test user plus product stock, an address, a card, favorites, and three sample orders in different statuses — so the account pages have data to show without doing a manual checkout first.

**Errors:** `GlobalExceptionHandler` (registered via `AddExceptionHandler` + `AddProblemDetails`) turns unhandled exceptions into a 500 `ProblemDetails`. Note it is registered but the `app.UseExceptionHandler()` call in `Program.cs` is currently commented out.

**API docs:** Uses **Scalar** (`MapScalarApiReference`), not Swagger UI. OpenAPI document via `AddOpenApi`/`MapOpenApi`. Both are Development-only.

**CORS:** A permissive `AngularAppPolicy` (any origin/header/method) plus a default policy bound to `AllowedOrigins` in config. `AngularAppPolicy` is the one applied in `Program.cs`.

## Conventions

- Primary-constructor DI throughout (`SomeService(ApplicationDbContext context)`), with a `private readonly` field assigned from the parameter.
- All service methods are `async` and take a trailing `CancellationToken`; reads that don't track use `.AsNoTracking()`.
- Nullable reference types and implicit usings are enabled.
- `WeatherForecast*` files are leftover template scaffolding and are not part of the domain.
