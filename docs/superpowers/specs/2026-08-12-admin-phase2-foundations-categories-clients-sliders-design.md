# Design: Admin Dashboard Phase 2 — Foundations, Categories, Clients & Sliders

**Date:** 2026-08-12
**Status:** Approved

## Goal

The Phase 1 design doc (`docs/superpowers/specs/2026-08-02-admin-phase1-auth-roles-admins-design.md`)
envisioned a 5-phase build: Categories/Clients → Products → Orders →
Dashboard/Reports. Sliders was listed in the reference mockup's sidebar and
had a permission key seeded, but was never assigned to a phase.

This phase closes that gap. It delivers every admin feature that is not
Products, Orders, or Dashboard/Reports — so no feature is left unowned — plus
two pieces of shared groundwork the remaining phases depend on:

1. **Audit + soft-delete foundation**, applied schema-wide.
2. **File upload infrastructure**, which does not exist in the backend today.
3. **Categories** admin management (hierarchical).
4. **Clients** admin management (customer accounts).
5. **Sliders** — green-field entity, backend and admin UI.
6. Two **drive-by fixes**: an unauthenticated write hole on
   `ProductsController`, and the unpopulated audit columns on the Phase 1
   Admin/Role write paths.

Remaining future phases: **Products**, **Orders**, **Dashboard/Reports** —
each its own design/plan cycle.

## Decisions (locked with user)

| Topic | Decision |
| --- | --- |
| Admin vs. site controllers | Admin-only functionality gets its **own new controller/service**. Existing site-facing controllers are only *updated in place* when the functionality is genuinely shared (e.g. the public category list stays on `CategoriesController`). |
| Audit fields scope | One **foundational pass across all real business entities**, so later phases inherit it already in place rather than re-migrating the same tables. |
| Audit FK target | Everything points to **`Admin`**, all FKs **nullable**. Customer self-service writes leave them null — this is an admin-action audit trail. |
| Soft-delete | `IsDeleted`/`DeletedOn`/`DeletedById` instead of hard deletes, enforced by a global EF query filter. |
| Category hierarchy UI | Flat table with a "Parent" column by default; a "Show tree" toggle switches to an indented, expand/collapsible view. |
| Image handling | **Build real file upload** this phase (not URL strings), stored on **local disk under `wwwroot`**, applied to **Sliders + Categories**. Products keeps URL strings until its own phase. |
| Orphan files | Replacing an image leaves the old file on disk. No cleanup job. |
| Slider fields | Minimal **plus scheduling** (`StartsOn`/`EndsOn`). |
| Clients capabilities | Admin can **view, edit, toggle active, and soft-delete** customer accounts. |
| Clients naming | **"Clients" everywhere** — permission keys, route, and UI label all agree. Diverges from the mockup's "Customers" wording deliberately. |
| Products hole | Gated now, even though the full admin Products feature is a later phase. |

---

## Foundation A: audit + soft-delete

### An interface, not just a base class

Rewrites the existing, currently-unused
`backend/Ecommerce/Entities/AuditableEntity.cs` (nothing inherits it today,
per the Phase 1 design doc note — safe to change rather than extend).

It must be an **`IAuditable` interface** with `AuditableEntity` as a
convenience base implementing it, because `ApplicationUser` already inherits
`IdentityUser` and C# allows only one base class — a base-class-only design
cannot include customer accounts at all:

```csharp
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

public abstract class AuditableEntity : IAuditable { /* the properties */ }
```

Everything keyed off auditing — the query-filter reflection loop and the
`SaveChanges` hook — tests against **`IAuditable`**, not `AuditableEntity`, so
both inheritance styles participate. (`ChangeTracker.Entries<IAuditable>()`
is valid: an interface satisfies the method's `where T : class` constraint.)

The FK type changes from `string` (FK to `ApplicationUser`) to `long?`
(FK to `Admin`), per the Phase 1 doc's instruction that catalog auditing
should point at `Admin`.

**Inherit `AuditableEntity`:** `Category`, `Product`, `ProductImage`, `Order`,
`OrderItem`, `Address`, `Card`, `Review`, `Admin`, `AdminRole`, and the new
`Slider`. Most of these are `sealed`, which is no obstacle — `sealed` blocks
being inherited *from*, not inheriting.

**Implements `IAuditable` directly:** `ApplicationUser` (declares the
properties itself, since its base slot is taken by `IdentityUser`).

**Duplicate `CreatedOn`.** `Order`, `Review`, and `Admin` already declare
their own `public DateTime CreatedOn { get; set; } = DateTime.UtcNow;`.
Each must **drop that declaration** and inherit the base's — same name, same
type, same default, so no behavior or column changes and no consumer breaks
(e.g. the `createdOn` field the Phase 1 Admins page reads). Leaving both in
place would be member hiding and would confuse EF's model building.

**Excluded (junctions/tokens/transient state):** `AdminRolePermission`,
`Permission`, `AdminRefreshToken`, `AdminPasswordResetToken`, `RefreshToken`,
`Favorite`, `Cart`, `CartItem`, `NewsletterSubscription` — joins or live
operational records where an audit trail adds nothing; removing a favorite or
a cart line stays a real delete.

### Stamping: reuse the existing hook, don't thread parameters

`ApplicationDbContext.SaveChangesAsync` (lines 48–68) **already** stamps
`CreatedById`/`UpdatedById`/`UpdatedOn` for auditable entries from
`ClaimTypes.NameIdentifier`. It is dead code today only because nothing
inherits the base. It is the correct hook and is already documented in
`backend/CLAUDE.md` as this repo's convention — so services do **not** take an
`adminId` parameter, and existing service signatures stay unchanged.

Three corrections to that hook:

1. **Parse to `long`, and only stamp for admins.** The claim holds an `Admin`
   id (`long`) for admin-scheme requests but an `ApplicationUser` GUID string
   for customer requests — a customer GUID must never land in an `Admin` FK.
   `long.TryParse` discriminates them cleanly: a GUID never parses as a long.
   On a failed parse (customer request, or unauthenticated), leave the audit
   columns null.
2. **Intercept deletes.** For `EntityState.Deleted` entries, flip the entry to
   `Modified`, set `IsDeleted = true`, `DeletedOn`, `DeletedById`. This means
   every existing `Remove()` call in every service becomes a soft delete with
   **no service code changes** — including `RoleService.DeleteAsync`.
3. **Override the synchronous `SaveChanges` too**, sharing one private helper,
   so sync saves don't bypass the logic.

### Query filter

`OnModelCreating` applies `HasQueryFilter(e => !e.IsDeleted)` to every entity
type implementing `IAuditable`, via a reflection loop over
`modelBuilder.Model.GetEntityTypes()` — soft-deleted rows disappear from every
query by default without each entity configuration repeating it.

This makes several existing checks correct for free: `RoleService`'s
"role name must be unique" query automatically stops counting soft-deleted
roles, so a deleted role's name can be reused.

Two known consequences to handle rather than discover later:

- **Required-navigation warning.** EF Core raises
  `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` when
  an unfiltered entity has a required navigation to a filtered one (e.g.
  `Favorite` → `Product`, `CartItem` → `Product`). Expected; suppress it
  explicitly in `DbContextOptionsBuilder.ConfigureWarnings` with a comment,
  rather than leaving noise in the build output.
- **Soft-deleted customers and Identity.** Filtering `ApplicationUser` means
  `UserManager` lookups skip soft-deleted users, so login rejects them
  automatically — which is the desired behavior. The flip side: a
  soft-deleted user's email *looks* free at registration, then fails on the
  unique index. Registration must check with `IgnoreQueryFilters()` and
  return a clear "email already in use" error.

No "view deleted / restore" screen is in scope; `IgnoreQueryFilters()` is
available for a future phase to build one.

### Migration

One EF Core migration adds the six columns plus their `Admin` FKs to all
twelve entities. Existing rows default to `IsDeleted = false` and
`CreatedById = null` (historical creator unknown). No backfill.

All FKs land as `DeleteBehavior.Restrict` — `OnModelCreating` already
rewrites every cascade FK to `Restrict`, so the new `Admin` FKs raise no
multiple-cascade-path problems.

---

## Foundation B: file upload

Nothing in the backend handles file uploads today — there is no `IFormFile`
reference anywhere, and `Category.Image` / `Product.Image` are plain strings.
`Program.cs` has no `UseStaticFiles()` and there is no `wwwroot` directory.

**New `IFileStorage` / `LocalFileStorage`** (registered `Scoped` in
`DependacyInjection.cs`):

```csharp
Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken ct);
```

- Writes to `wwwroot/uploads/<module>/<guid><ext>`; returns the relative
  public path (`/uploads/categories/<guid>.jpg`) which is what gets stored in
  the entity's `Image` column — same column type as today, so nothing
  downstream changes.
- Validates extension against an allowlist (`.jpg`, `.jpeg`, `.png`, `.webp`),
  content type, and a max size (2 MB), returning a `Result` failure with a new
  `FileErrors` entry rather than throwing.
- Creates the target directory on demand.
- Replacing an image writes a new file and leaves the old one — deliberate,
  since a soft-deleted record may be restored later.

**Wiring:** add `app.UseStaticFiles()` to `Program.cs` (before
`UseAuthorization`; uploaded images are public), create
`wwwroot/uploads/.gitkeep`, and gitignore `wwwroot/uploads/*` except that file
so uploaded content never enters version control.

**Request shape:** admin create/update DTOs take an optional
`IFormFile? ImageFile` alongside the existing `string? Image`. If `ImageFile`
is present it is saved and its path wins; otherwise `Image` is kept as-is.
That keeps "leave the current image alone" expressible on an update, and
existing seeded URL values keep working.

Products is intentionally excluded: no admin Products UI ships this phase, so
uploader support there would be unexercised backend code.

---

## Categories

**Backend** — new `AdminCategoriesController` at `api/Admin/Categories`,
`[Authorize(AuthenticationSchemes = "AdminBearer")]`:

- `GET ""`, `GET "{id}"` — `[HasPermission(PermissionKeys.CategoriesView)]`
- `POST ""`, `PUT "{id}"`, `DELETE "{id}"`, `PUT "{id}/toggleStatus"` —
  `[HasPermission(PermissionKeys.CategoriesManage)]`

It reuses the existing `ICategoryService`/`CategoryService` — same entity,
same validation rules, no duplicated logic. `AddAsync`/`UpdateAsync` gain
`IFileStorage` handling for `ImageFile`; `DeleteAsync` needs no change (the
DbContext hook turns its `Remove()` into a soft delete).

The public `CategoriesController` keeps only `GET ""`/`GET "{id}"` for the
storefront (unauthenticated, behavior unchanged) and **loses its write
actions** — those are unauthenticated today, which is the hole this closes.
Both controllers get consistent `ApiResponse<T>` wrapping (currently mixed —
see `backend/CLAUDE.md`).

**Frontend** — replaces three dead pre-Phase-1 stubs:
`features/pages/categories/categories.ts` (currently
`<p>categories works!</p>`), `core/services/category-services.ts` (a
non-functional `@Service()` stub), and `shared/interface/categoryInterface.ts`.
Follows the Phase 1 Admins/Roles pages as the pattern.

Route `/admin/categories` requires `categories.view` via
`adminPermissionGuard`; the component additionally checks `categories.manage`
to show or hide Create/Edit/Delete/Toggle, so a view-only role sees a
read-only table. New `NAV_ITEMS` entry.

**Hierarchy UI:** default flat table with a "Parent" column (parent title, or
"—" for top-level) and a parent-picker dropdown in the create/edit form. A
"Show tree" toggle switches to an indented, expand/collapsible view built
client-side by grouping on `ParentId`.

The stray nested `features/pages/categories/products/` stub is left alone —
it gets sorted out when the Products phase starts.

---

## Clients

Admin management of customer (`ApplicationUser`) accounts. Nothing exists
today — no admin controller, service, or UI.

**Naming:** "Clients" throughout — `clients.view`/`clients.manage` (already
seeded), route `/admin/clients`, sidebar label "Clients". The reference
mockup says "Customers"; we diverge so keys, route, and label agree.

**Backend** — new `AdminClientsController` at `api/Admin/Clients` and a new
`IClientService`/`ClientService`:

- `GET ""` — list with search (name/email) and paging —
  `[HasPermission(ClientsView)]`
- `GET "{id}"` — detail, including order count and lifetime total —
  `[HasPermission(ClientsView)]`
- `PUT "{id}"` — edit `FirstName`/`LastName`/`Email`/`PhoneNumber` —
  `[HasPermission(ClientsManage)]`
- `PUT "{id}/toggleStatus"` — enable/disable — `[HasPermission(ClientsManage)]`
- `DELETE "{id}"` — soft delete — `[HasPermission(ClientsManage)]`

Two details that make this non-trivial:

- **Toggle uses Identity's built-in lockout**, not a new column:
  `LockoutEnabled = true` + `LockoutEnd = DateTimeOffset.MaxValue` to disable,
  `LockoutEnd = null` to enable. Login already honors lockout, so no auth
  changes are needed. The DTO exposes it as a plain `isActive` boolean.
- **Email edits go through `UserManager`** (`SetEmailAsync` /
  `SetUserNameAsync`), never by assigning the property directly — Identity
  keeps `NormalizedEmail`/`NormalizedUserName` in sync and enforces
  uniqueness, and a direct assignment would silently desync them and break
  login.

**Soft-deleting a client leaves their orders.** `Order` snapshots
`ShipToName`/`ShipToPhone` and each `OrderItem` snapshots product title, SKU
and price, so an order stays fully readable after its customer is deleted.
But the global filter means `Include(o => o.User)` returns null for those
orders, so any admin view joining to the user must tolerate a null
`User` and fall back to the order's snapshot fields. The Orders phase must
honor this; it is called out here because this phase creates the condition.

**Frontend** — new `features/pages/clients/`, `core/services/client-services.ts`,
`shared/interface/client-interfaces.ts`. Table with search, an active/disabled
pill toggle, edit modal, delete confirm — same shape as the Admins page. Route
guarded on `clients.view`, write controls gated on `clients.manage`.

---

## Sliders

Green-field: no entity, controller, service, or UI exists. `sliders.manage`
is seeded but unconsumed.

**New `Slider` entity:** `Id (long)`, `Title`, `Image` (path from
`IFileStorage`), `Link` (target URL, nullable), `Sort (int?)`,
`Status (bool)`, `StartsOn (DateTime?)`, `EndsOn (DateTime?)`, plus the audit
base. It gets its own `AddSliders` migration, generated on top of Foundation
A's `AddAuditAndSoftDelete`, because the implementation is split into two
plans (2A foundations, 2B features) and Sliders belongs to the second.

**Permission key gap:** the catalog has `SlidersManage` but no
`SlidersView`, so a view-only slider role is impossible. Add
`SlidersView = "sliders.view"` to `PermissionKeys.Catalog`. **No migration
needed** — `AdminDataSeeder` inserts any catalog key not already present and
grants new permissions to Super Admin, so it seeds itself on the next dev run.

**Backend:**

- `AdminSlidersController` at `api/Admin/Sliders` — full CRUD plus
  `PUT "{id}/toggleStatus"`, `AdminBearer`, gated `sliders.view` /
  `sliders.manage`.
- Public `SlidersController` at `api/Sliders` — `GET ""` only,
  unauthenticated, returning **active and currently-scheduled** sliders
  ordered by `Sort`: `Status == true`, `StartsOn == null || StartsOn <= now`,
  `EndsOn == null || EndsOn >= now`. Scheduling is evaluated server-side so
  the storefront needs no date logic.

**Frontend (admin only):** new `features/pages/sliders/` with the same
table/modal shape as Categories, an image uploader, and start/end date
inputs. Route guarded on `sliders.view`, writes on `sliders.manage`.

**Storefront slider carousel is out of scope** — this phase ships the admin
management and the public endpoint that feeds it, not the customer-facing
component.

---

## Drive-by fixes

**Products write hole.** `ProductsController`'s `POST`/`PUT`/`DELETE`/
`toggleStatus` are unauthenticated today — the same gap Categories had. They
get `[Authorize(AuthenticationSchemes = "AdminBearer")]` +
`[HasPermission(PermissionKeys.ProductsManage)]`. `Product` inherits the audit
base in this phase's migration, so stamping and soft-delete come free from the
DbContext hook. No admin Products UI, no `AdminProductsController` — that is
the Products phase.

**Phase 1 audit columns.** `Admin` and `AdminRole` inherit the audit base, and
because stamping and soft-delete both live in the DbContext hook, the existing
`AdminService`/`RoleService` create/update/delete paths populate the new
columns with **no code changes**. `RoleService.DeleteAsync` (fixed in commit
`a676da5`) becomes a soft delete automatically, and its unique-name check
starts excluding soft-deleted roles automatically via the query filter. Only
verification is needed here, not new logic.

---

## Out of scope for this phase

- **Products**, **Orders**, and **Dashboard/Reports** admin features — the
  three remaining phases.
- A "view deleted / restore" UI for any soft-deleted entity.
- Cloud/blob file storage, image resizing or thumbnails, and orphan-file
  cleanup.
- Storefront-facing slider carousel.
- Category drag-to-reorder and bulk operations (`Sort` exists as a field; no
  reordering UI).
- Admin editing of customer passwords, addresses, cards, or orders.
