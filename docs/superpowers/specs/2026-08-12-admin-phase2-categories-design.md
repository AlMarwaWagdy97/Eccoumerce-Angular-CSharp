# Design: Admin Dashboard Phase 2 — Audit/Soft-Delete Foundation & Categories

**Date:** 2026-08-12
**Status:** Approved

## Goal

The Phase 1 design doc (`docs/superpowers/specs/2026-08-02-admin-phase1-auth-roles-admins-design.md`)
originally envisioned Categories/Clients, Products, Orders, and
Dashboard/Reports as three further separate phases. This phase deliberately
narrows that down to just **Categories**, plus a piece of shared groundwork
both this and later phases depend on:

- A **foundational audit + soft-delete base**, applied schema-wide.
- Full **admin Categories management** (view/create/update/delete/toggle),
  including the hierarchical parent/child structure `Category` already has.
- Two small **drive-by fixes** surfaced by the codebase survey that are cheap
  to do now and risky to leave: an unauthenticated write hole on
  `ProductsController` (identical to one Categories had), and unpopulated
  audit columns on the Phase 1 Admin/Role write paths.

Products, Orders, Sliders, Clients, and Dashboard/Reports remain their own
future design/plan cycles, per the original Phase 1 breakdown — this phase
does not build their screens.

## Decisions (locked with user)

| Topic | Decision |
| --- | --- |
| Admin vs. site controllers | Admin-only functionality gets its **own new controller/service** (e.g. `AdminCategoriesController`). Existing site-facing controllers are only *updated in place* when the functionality is genuinely shared (e.g. the public category list stays on `CategoriesController`). |
| Audit fields scope | Not Category-only — applied as **one foundational pass across all real business entities** in the schema, so later phases (Products, Orders, ...) inherit it already in place rather than each re-migrating the same tables. |
| Audit FK target | Everything points to **`Admin`** (not `ApplicationUser`), and all three FKs (`CreatedById`/`UpdatedById`/`DeletedById`) are **nullable**. Customer self-service writes (place an order, save an address, add a favorite) leave them null — this is an admin-action audit trail, not a general one. |
| Soft-delete | All entities in scope get `IsDeleted`/`DeletedOn`/`DeletedById` instead of hard deletes, enforced via a global EF query filter. |
| Category hierarchy UI | Default **flat table** with a "Parent" column; a "Show tree" toggle switches to an indented, expand/collapsible nested view. |
| Products hole | Gated now (`AdminBearer` + `products.manage` on the existing write actions) even though the full admin Products feature is a later phase. |
| Admin/Role stamping | `AdminService`/`RoleService` (built in Phase 1) are updated now to populate the new audit columns on their existing create/update/delete paths, rather than leaving them null until some later phase touches those files again. |

## Foundation: audit + soft-delete base

Repurposes the existing, currently-unused `backend/Ecommerce/Entities/AuditableEntity.cs`
(nothing inherits it today, per the Phase 1 design doc note — safe to rewrite
rather than additive):

```csharp
public abstract class AuditableEntity
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

**Entities that inherit it:** `Category`, `Product`, `ProductImage`, `Order`,
`OrderItem`, `Address`, `Card`, `Review`, `Admin`, `AdminRole`,
`ApplicationUser`.

**Excluded (junctions/tokens/transient state):** `AdminRolePermission`,
`Permission`, `AdminRefreshToken`, `AdminPasswordResetToken`, `RefreshToken`,
`Favorite`, `Cart`, `CartItem`, `NewsletterSubscription` — joins or live
operational records where an audit trail doesn't add value; removing a
favorite or cart line stays a real `Remove()`.

**Query filter:** `ApplicationDbContext.OnModelCreating` applies
`HasQueryFilter(e => !e.IsDeleted)` to every entity type assignable to
`AuditableEntity` via a reflection loop over `modelBuilder.Model.GetEntityTypes()`,
so soft-deleted rows disappear from every query by default without each
entity configuration repeating the filter. No "view deleted / restore" screen
exists yet — `IgnoreQueryFilters()` is available for a future phase to build
one, but it's out of scope here.

**Migration:** one EF Core migration adds the six new columns (plus their
`Admin` FKs) to all eleven entities above. Pre-existing rows get
`IsDeleted = false`, `CreatedById = null` (historical creator unknown) —
no backfill beyond defaults.

**`AdminRole` behavior change:** `RoleService.DeleteAsync` (just fixed in
commit `a676da5` to correctly remove roles with assigned permissions) changes
from a real delete to setting `IsDeleted = true` + stamping `DeletedById`.
The existing "role name must be unique" check must also filter out
soft-deleted roles so a deleted role's name can be reused.

## Backend: Categories admin feature

- New `AdminCategoriesController` at `api/Admin/Categories`
  (`[Authorize(AuthenticationSchemes = "AdminBearer")]`):
  - `GET ""`, `GET "{id}"` — `[HasPermission(PermissionKeys.CategoriesView)]`
  - `POST ""`, `PUT "{id}"`, `DELETE "{id}"`, `PUT "{id}/toggleStatus"` —
    `[HasPermission(PermissionKeys.CategoriesManage)]`
- Reuses the existing `ICategoryService`/`CategoryService` for the actual
  CRUD logic rather than duplicating it — same entity, same validation rules.
  Its mutating methods (`CreateAsync`, `UpdateAsync`, `DeleteAsync`,
  `ToggleStatusAsync`) gain a `long adminId` parameter (read from the
  `AdminBearer` token's `NameIdentifier` claim in the controller) to stamp
  `CreatedById`/`UpdatedById`/`DeletedById`. `DeleteAsync` becomes a
  soft-delete.
- The existing public `CategoriesController` keeps only `GET ""`/`GET "{id}"`
  for the storefront (unauthenticated, unchanged behavior) and **loses its
  write actions** — those were unauthenticated today, which is the security
  gap this phase closes. Both controllers get consistent `ApiResponse<T>`
  wrapping (currently inconsistent — see `backend/CLAUDE.md`).
- New `Errors/` entries as needed follow the existing per-domain `*Errors`
  pattern; DTOs stay in `Contracts/Categories/` (existing `CategoryRequest`/
  `CategoryResponse` are reused, no shape change needed for the admin side).

## Backend: Products drive-by fix

`ProductsController`'s `POST`/`PUT`/`DELETE`/`toggleStatus` actions get
`[Authorize(AuthenticationSchemes = "AdminBearer")]` +
`[HasPermission(PermissionKeys.ProductsManage)]`, and `ProductService`'s
corresponding methods gain the same `adminId`-stamping and soft-delete
treatment as `CategoryService`, since `Product` also inherits the audit base
in this phase's migration. No new admin UI, no `AdminProductsController` —
just closing the hole. The full admin Products feature (dedicated controller,
admin UI, variants/stock management, etc.) remains its own future phase.

## Frontend (`frontend/src/app/admin/`)

- Replace the dead pre-Phase-1 stubs: `features/pages/categories/categories.ts`
  (currently `<p>categories works!</p>`), `core/services/category-services.ts`
  (currently a non-functional `@Service()` stub), and
  `shared/interface/categoryInterface.ts`, following the pattern established
  by the Phase 1 Admins/Roles pages (signals-based service, table, create/edit
  form, delete confirm) rather than the `site/` category service (similar
  shape, but needs `ApiResponse<T>` unwrapping via the admin bearer-token
  interceptor path).
- Route `/admin/categories` requires `categories.view` (`adminPermissionGuard`)
  to load the page at all; the component additionally checks
  `categories.manage` client-side to show/hide the Create/Edit/Delete/Toggle
  controls, so a view-only role sees a read-only table. New `NAV_ITEMS` entry
  in `main-layout.ts` alongside Dashboard/Roles/Admins.
- **Hierarchy UI:** default view is a flat table with a "Parent" column
  (parent category title, or "—" for top-level categories) and a parent
  picker (dropdown of existing categories) in the create/edit form. A
  "Show tree" toggle switches the table to an indented, expand/collapsible
  nested view built from the same data (grouped client-side by `ParentId`).
- The stray nested `features/pages/categories/products/` stub (an artifact of
  pre-Phase-1 scaffolding, unrelated to Categories) is left untouched — it
  gets sorted out when the Products phase starts.

## Out of scope for this phase

- Products, Orders, Sliders, Clients, and Dashboard/Reports screens/features
  — each remains its own future design/plan cycle.
- A "view deleted / restore" UI for any soft-deleted entity.
- Multi-level category nesting limits or drag-to-reorder — `Sort` already
  exists as a field; reordering UI isn't part of this pass.
- Bulk category operations (bulk delete/move).
