# Plan Phases — Status Tracker

**Last updated:** 2026-08-15
**Current branch:** `phase2a-audit-soft-delete` (10 commits, `463ffd8`..`052254e`, not merged to `main`)
**Currently doing:** nothing in flight — Phase 2A is complete; Phase 2B is ready to start

Legend: ✅ Done · 🔵 Doing now · ⬜ Not started · ⛔ Blocked

> Keep this file updated as work progresses: change the status cell, update
> **Last updated** / **Currently doing** at the top, and note the evidence
> (commit hash, file, or test) that proves a task is really done.

---

## 1. Phases (high level)

| # | Phase | Plan / spec | Status |
|---|---|---|---|
| 0 | Storefront + Account features (auth, orders, favorites, addresses, cards) | `docs/superpowers/specs/2026-07-14-account-features-design.md` | ✅ Done |
| 0 | Monorepo merge (backend + frontend via git subtree) | `docs/superpowers/specs/2026-07-09-monorepo-merge-design.md` | ✅ Done |
| 1 | Admin: auth, roles & permissions, admins | `docs/superpowers/plans/2026-08-02-admin-phase1-auth-roles-admins.md` (20 tasks) | ✅ Done — all 20 tasks committed + 2 follow-up bug fixes |
| **2A** | **Foundations: audit trail, soft-delete, file upload** | `docs/superpowers/plans/2026-08-12-admin-phase2a-foundations.md` (9 tasks) | ✅ **Done** — 9/9 tasks + 1 follow-up defect fix |
| 2B | Categories, Clients, Sliders | `docs/superpowers/plans/2026-08-12-admin-phase2b-categories-clients-sliders.md` (10 tasks) | ⬜ Not started — **unblocked**, 2A's deliverables all exist |
| 3 | Products admin | not yet designed | ⬜ Not started |
| 4 | Orders admin | not yet designed | ⬜ Not started |
| 5 | Dashboard / Reports | not yet designed | ⬜ Not started |

---

## 2. Phase 2A — Audit, Soft-Delete & File Upload Foundations

Progress: **9 done · 0 remaining** · 72 backend tests pass · build clean

| Task | What it delivers | Status | Evidence |
|---|---|---|---|
| 1 | `IAuditable` / `AuditableEntity` + adoption across 11 entities | ✅ Done | `463ffd8`, `AuditableEntityTests.cs` (13 tests) |
| 2 | EF migration `AddAuditAndSoftDelete` | ✅ Done | `1e1479b` — 74 columns, 33 FKs, all `Restrict`; applied to the dev DB |
| 3 | Global `!IsDeleted` query filter in `OnModelCreating` | ✅ Done | `bfd30c8`, `SoftDeleteQueryFilterTests.cs` (3 tests) |
| 4 | Audit stamping + delete→soft-delete in `SaveChanges` | ✅ Done | `89a760a`, `AuditStampingTests.cs` (5 tests) |
| 5 | Registration blocks emails owned by soft-deleted accounts | ✅ Done | `fc34ebb`, `AuthServiceRegistrationTests.cs` |
| 6 | `IFileStorage` + `LocalFileStorage` + `FileErrors` | ✅ Done | `73d3920`, `LocalFileStorageTests.cs` (7 tests) |
| 7 | Serve uploads (`wwwroot/uploads`, `UseStaticFiles`) | ✅ Done | `3ff6e04` — verified by fetching `/uploads/ping.txt` over HTTPS |
| 8 | Lock down Products write endpoints (`products.manage`) | ✅ Done | `14589bb`, `ProductsControllerAuthorizationTests.cs` (6 tests) — `DELETE` → 401, `GET` → 200 |
| 9 | Verify Phase 1 admin paths inherited soft-delete | ✅ Done | `bee6b47`, `RoleServiceSoftDeleteTests.cs` (3 tests); `RoleService.cs` unchanged |
| + | **Follow-up:** filtered unique indexes on `IsDeleted` | ✅ Done | `052254e` + migration `AddFilteredUniqueIndexes` |

**Done criteria — all met**
- ✅ `dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj` — 72/72 pass.
- ✅ API boots, dev seeders run, `/uploads/<file>` is fetchable.
- ✅ `DELETE https://localhost:7297/api/Products/1` returns 401 without an admin token.
- ✅ Deleting anything auditable sets `IsDeleted` instead of removing the row — confirmed
  in SQL Server: deleted `AdminRoles` rows persist with `IsDeleted=1`, `DeletedOn`, and
  `CreatedById`/`DeletedById` = the acting admin, while the API list shows only live rows.

### Deviations from the plan as written (all committed)

1. **Explicit audit-navigation configuration.** EF cannot resolve `Admin`'s three
   self-referencing audit navigations by convention — model building throws. `OnModelCreating`
   now configures `CreatedBy`/`UpdatedBy`/`DeletedBy` explicitly for every `IAuditable` type,
   all `Restrict`. `Slider` will be picked up by that loop automatically in 2B Task 7.
2. **Early `SaveChangesAsync` parse fix.** Task 1 could not compile until the existing hook
   stopped assigning the `NameIdentifier` claim string into the now-`long?` columns; Task 4
   then replaced the method wholesale as specified.
3. **Filtered unique indexes** (see below).

### ⚠️ Testing limitation worth carrying into 2B

The whole suite runs on the **EF InMemory provider, which does not enforce unique indexes**.
That is how the soft-delete/unique-index conflict reached a running app with a green suite:
`RoleServiceSoftDeleteTests` asserted a deleted role's name is reusable and passed, while the
same operation returned **HTTP 500** against SQL Server. Fixed in `052254e` by filtering the
seven affected indexes on `[IsDeleted] = 0`. **Any uniqueness rule added in 2B (category
slugs, slider fields) must be checked against SQL Server by hand — a passing test proves
nothing here.**

---

## 3. Phase 2B — Categories, Clients & Sliders

Progress: **0 done · 10 remaining** — no longer blocked

| Task | Area | Deliverable | Status |
|---|---|---|---|
| 1 | Backend · Categories | Image upload + parent validation in `CategoryService` | ⬜ Not started |
| 2 | Backend · Categories | `AdminCategoriesController`; public `CategoriesController` → read-only | ⬜ Not started |
| 3 | Frontend · Categories | Categories admin page (table + tree view) | ⬜ Not started |
| 4 | Backend · Clients | `IClientService` / `ClientService` over `ApplicationUser` | ⬜ Not started |
| 5 | Backend · Clients | `AdminClientsController` | ⬜ Not started |
| 6 | Frontend · Clients | Clients admin page | ⬜ Not started |
| 7 | Backend · Sliders | `Slider` entity, EF config, `sliders.view` permission, `AddSliders` migration | ⬜ Not started |
| 8 | Backend · Sliders | `ISliderService` / `SliderService` | ⬜ Not started |
| 9 | Backend · Sliders | `AdminSlidersController` + public `SlidersController` | ⬜ Not started |
| 10 | Frontend · Sliders | Sliders admin page | ⬜ Not started |

**Sequencing constraint:** Tasks 3, 6 and 10 each edit `frontend/src/app/app.routes.ts`,
`app.routes.server.ts` and `admin/features/layouts/main-layout/main-layout.ts` — run them
sequentially, never in parallel.

**What 2B can now rely on (delivered by 2A):**
`IAuditable`/`AuditableEntity`; automatic stamping and soft-delete inside `SaveChanges`
(so **no service takes an `adminId`** and a plain `Remove()` is already a soft delete);
the global `!IsDeleted` filter; `Ecommerce.Storage.IFileStorage.SaveAsync(IFormFile, string, CancellationToken)`
returning `/uploads/<module>/<guid><ext>`; `Ecommerce.Errors.FileErrors`; `UseStaticFiles()`
with `wwwroot/uploads/`; and permission-gated `ProductsController` writes.

---

## 4. Explicitly out of scope for Phase 2

- Products, Orders, Dashboard/Reports admin features (Phases 3–5).
- "View deleted / restore" UI for soft-deleted entities.
- Cloud/blob storage, image resizing, thumbnails, orphan-file cleanup.
- Storefront-facing slider carousel.
- Category drag-to-reorder and bulk operations.
- Admin editing of customer passwords, addresses, cards, or orders.

---

## 5. Next action

Phase 2A is finished. Choose one:

1. **Merge 2A.** Branch `phase2a-audit-soft-delete` is 10 commits ahead of `main` and has
   never been merged. Nothing on it is frontend-facing, so the storefront is unaffected.
2. **Start Phase 2B Task 1** (`CategoryService` image upload) from
   `docs/superpowers/plans/2026-08-12-admin-phase2b-categories-clients-sliders.md`.

Housekeeping note: the dev database holds two soft-deleted `Temp QA` roles (ids 3 and 5)
left by the SQL-Server verification, and `frontend/src/app/site/core/guards/auth-guard.ts`
has an uncommitted edit on `main` that predates this work and was never staged.
