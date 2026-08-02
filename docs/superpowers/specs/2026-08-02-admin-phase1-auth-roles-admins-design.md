# Design: Admin Dashboard Phase 1 — Authentication, Roles & Admin Management

**Date:** 2026-08-02
**Status:** Approved

## Goal

Build the foundation for a new Admin Dashboard, styled after the reference
design at `https://envntcommerce.lovable.app/admin`, starting with:

- Admin login, logout, and password reset (via real email).
- Roles & Permissions CRUD — roles with granular, per-module permission toggles.
- Admins CRUD — manage admin users and assign a role to each.

This is Phase 1 of a 5-phase admin dashboard build (Categories/Clients →
Products → Orders → Dashboard/Reports follow in later phases, each with its
own design/plan cycle). Phase 1 must ship the auth + authorization foundation
the later phases build their permission checks on top of.

## Reference design — what it is and isn't

`https://envntcommerce.lovable.app/admin` (reached via the storefront's
"Switch to Admin" button — direct navigation to `/admin` 404s because it's a
client-routed SPA) is a **static Lovable mockup with no real backend**:

- Left sidebar nav: Dashboard, Categories, Products, Orders, Sliders,
  Customers, Users. No separate Roles/Admins nav items — it has one flat
  "Users" list with a `Role` dropdown (`Admin`/`Customer`) and no granular
  permissions UI at all.
- Its "Add User" modal has no password field — confirming it's UI-only, not
  wired to real auth.
- Visual language: cream page background (~`#F7F4EE`), forest-green accent
  (~`#2C5545`) on the active nav pill and primary buttons, white sidebar,
  card-based dashboard tiles with a trend icon, tables with avatar-initial
  circles, pill-shaped status toggles, role badges (dark pill for admin-type
  roles, light beige pill otherwise), search input + dropdown filter row
  above tables, icon-only edit/delete actions per row, right-aligned
  slide-in-style modals for create/edit forms with a green primary button.
- This palette is not new to this codebase — it already matches the
  forest-green/cream tokens introduced for the customer account area
  (see `docs/superpowers/specs/2026-07-14-account-features-design.md`).

**Conclusion:** mirror the reference faithfully for layout, color, spacing,
and component style (sidebar shell, cards, tables, modals, badges, toggles).
Do **not** mirror its data model — it has no real granular-permissions system
to copy, so that part is designed fresh below.

## Decisions (locked with user)

| Topic | Decision |
| --- | --- |
| Admin identity | **Separate `Admin` table**, fully independent of the customer `ApplicationUser`/Identity table. Its own login, its own JWT. |
| Permissions | **Fully dynamic**: `Permission` is a real DB table (not hardcoded C# constants or Identity `RoleClaims`), joined to roles via `AdminRolePermission`. |
| Password reset | **Real email**, via SMTP now (not deferred, not a dev-only token echo). |
| Email provider | **Mailtrap** sandbox for now (catches mail without real delivery; swappable later). Credentials supplied via user secrets, same pattern as `Jwt:Key`. |
| Admin login | **Dedicated** `api/Admin/Auth/*` endpoints and a dedicated `/admin/auth/login` frontend route — mirrors the existing separate `site/`/`admin/` frontend trees. |

## Data model

New entities under `backend/Ecommerce/Entities/`:

- **`Admin`** — `Id (long)`, `FirstName`, `LastName`, `Email` (unique),
  `PasswordHash`, `PhoneNumber?`, `IsActive`, `RoleId` (FK to `AdminRole`),
  `CreatedOn`. One role per admin (matches the reference's single `Role`
  column; extend to multi-role later only if a real need appears).
- **`AdminRole`** — `Id`, `Name`, `Description`, `IsSystem` (bool; protects a
  built-in "Super Admin" role from being deleted or edited down to nothing).
- **`Permission`** — `Id`, `Key` (dot-notation, e.g. `products.manage`,
  `orders.view`), `Module` (e.g. `"Products"`), `Description`. Seeded via
  migration/dev-seeder; **not** end-user-creatable in Phase 1 — the user
  asked for *Roles* CRUD with permission *toggles*, not a permissions-catalog
  editor. Seed keys for all 5 phases now (dashboard, categories, products,
  orders, clients, roles, admins, reports, sliders) since it's inert lookup
  data — no need to re-migrate per phase.
- **`AdminRolePermission`** — join table (`AdminRoleId`, `PermissionId`).
- **`AdminRefreshToken`** — mirrors the existing `RefreshToken` entity
  (`Token`, `ExpiresOn`, `RevokedOn`), scoped to `Admin` instead of
  `ApplicationUser`.
- **`AdminPasswordResetToken`** — `Token`, `AdminId`, `ExpiresOn`, `UsedOn`.

### Existing-code note (not a Phase 1 task, flagged for later)

`AuditableEntity` (`CreatedById`/`UpdatedById` FK'd to `ApplicationUser`) is
defined in `backend/Ecommerce/Entities/AuditableEntity.cs` but **no entity
currently inherits it** — `backend/CLAUDE.md` describes it as active
machinery, but it's dead code today. Phase 1 doesn't touch `Category` or
`Product`, so this isn't blocking. When Phase 2/3 wires `AuditableEntity`
onto `Category`/`Product`, it should point at `Admin`, not `ApplicationUser`,
since only admins manage catalog data going forward.

## Backend: admin auth subsystem

New `AdminAuthController` at `api/Admin/Auth`: `login`, `logout`,
`forgot-password`, `reset-password`, `refresh`. A new `IAdminJwtProvider`
issues tokens carrying the admin's id, name, role name, and one `permission`
claim per granted permission key — so both backend authorization and
frontend UI-gating read permissions straight off the token without an extra
round-trip.

Authorization uses a custom `[HasPermission("products.manage")]`
policy-based attribute (ASP.NET Core's dynamic-policy-provider pattern),
not `[Authorize(Roles=...)]`, since access is permission-based.

`forgot-password` always returns 200 regardless of whether the email exists
(avoids account enumeration), and emails a reset link via a new
`IEmailSender` abstraction (Mailtrap SMTP implementation; settings in
`appsettings.json` + user secrets, same pattern as the existing `Jwt:Key`).
`reset-password` validates the token (exists, unused, unexpired), updates
the password hash, marks the token used, and revokes all of that admin's
active refresh tokens (forces re-login everywhere as a safety measure).

## Backend: Roles & Permissions CRUD, Admins CRUD

- `RolesController` (`api/Admin/Roles`): list/get/create/update/delete, plus
  `GET api/Admin/Permissions` (the catalog, grouped by module) so the
  frontend can render the toggle grid. Deleting or editing a role with
  `IsSystem = true` is rejected via `Result`/`RoleErrors`.
- `AdminsController` (`api/Admin/Admins`): list/get/create/update/delete/
  toggle-active, assigning one `RoleId` per admin.
- Both follow existing repo conventions: thin controllers → `Scoped`
  services returning `Result`/`Result<T>`, `ApiResponse<T>` envelope,
  DTOs as `record`s with FluentValidation validators, Mapster mapping,
  new `Errors/RoleErrors.cs` and `Errors/AdminErrors.cs`.

## Frontend (`frontend/src/app/admin/`)

- Replace the current stub `main-layout` (navbar+footer) with a sidebar
  shell matching the reference: collapsible left sidebar, logo, icon nav
  with filled-pill active state, bottom profile card + logout, cream
  (`#F7F4EE`) content area, forest-green (`#2C5545`) accents — reusing the
  tokens already established for the customer account area.
- New `AdminAuthServices` (mirrors the customer `AccountServices` auth
  slice): login/logout/forgot/reset, storing the admin session under its
  **own** localStorage key (`shopdemo_admin_auth`) separate from the
  customer's `shopdemo_auth`, so one browser can hold both sessions at once.
- New `adminAuthGuard` protecting `/admin/**` except `/admin/auth/**`, plus a
  lightweight permission check per route (e.g. `/admin/admins` requires
  `admins.manage`) that redirects with a "not authorized" state if missing.
- The shared top-level `api.interceptor.ts` starts branching by URL prefix:
  requests to `api/Admin/**` attach the admin bearer token; everything else
  keeps attaching the customer token as today.
- Build out the existing empty stubs (`admin/features/auth/login`,
  `admin/features/pages/admins`) into real pages; add new
  `admin/features/auth/forgot-password`, `reset-password`, and
  `admin/features/pages/roles`.

## Dev bootstrap

Extend the existing dev-only `DataSeeder` (or a sibling `AdminDataSeeder`,
same `IsDevelopment()`-gated pattern) to seed the full permission catalog, a
built-in "Super Admin" role with every permission granted, and one seeded
Admin account — mirroring the existing `seed.tester@example.com` customer
seed.

## Out of scope for Phase 1

- Admin self-registration — admins are only created via the Admins CRUD by
  another admin with `admins.manage`.
- Two-factor authentication.
- An audit-log UI (the `AdminRefreshToken`/timestamp data could support one
  later, but no screen for it now).
- Multi-role-per-admin.
- The Dashboard, Categories, Products, Orders, Sliders, Customers screens
  themselves — those are Phases 2–5.
