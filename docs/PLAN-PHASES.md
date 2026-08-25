# Plan Phases — Status Tracker

**Last updated:** 2026-08-25
**Current branch:** `main`
**Currently doing:** Phase 4 (Orders admin) is complete — all 3 tasks done, final whole-branch review passed, merged to `main` and pushed to `origin/main`. Ready to move to Phase 5 (Dashboard/Reports) — the last phase on the original roadmap.

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
| 2B | Categories, Clients, Sliders | `docs/superpowers/plans/2026-08-12-admin-phase2b-categories-clients-sliders.md` (10 tasks) | ✅ **Done** — 10/10 tasks + closing checks + 1 follow-up defect fix + 1 build-budget fix |
| 3 | Products admin | `docs/superpowers/specs/2026-08-20-admin-phase3-products-design.md`, `docs/superpowers/plans/2026-08-20-admin-phase3-products.md` (4 tasks) | ✅ **Done** — 4/4 tasks + final whole-branch review + 1 follow-up fix round; merged to `main` (`736bbba..0f772de`), pushed to `origin/main` |
| 4 | Orders admin | `docs/superpowers/specs/2026-08-24-admin-phase4-orders-design.md`, `docs/superpowers/plans/2026-08-25-admin-phase4-orders.md` (3 tasks) | ✅ **Done** — 3/3 tasks + final whole-branch review + 3 follow-up fix rounds; merged to `main` (`86549e2..5f065b0`), pushed to `origin/main` |
| 5 | Dashboard / Reports | not yet designed | ⬜ Not started |

---

## 2. Phase 2A — Audit, Soft-Delete & File Upload Foundations


Progress: **9 done · 0 remaining** · 76 backend tests pass · build clean

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
| + | **Follow-up:** deferred cascade + product dependant cleanup | ✅ Done | `f04a81d`, `ProductSoftDeleteCleanupTests.cs` (4 tests) |

**Done criteria — all met**
- ✅ `dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj` — 76/76 pass.
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
4. **Deferred cascade timing.** `Remove()` on a soft-deletable entity threw
   `InvalidOperationException: the association ... has been severed` whenever EF already had a
   dependent tracked — EF processes the cascade the moment the state flips to `Deleted`, which is
   before `SaveChanges` and therefore before `ApplyAuditRules` can rewrite the delete into an
   update. `ApplicationDbContext` now sets `ChangeTracker.CascadeDeleteTiming =
   CascadeTiming.OnSaveChanges` so the rewrite lands first. Doing that meant writing the
   context's constructor out in full (`ChangeTracker` is instance-only), a deliberate departure
   from `backend/CLAUDE.md`'s primary-constructor convention.

### ⚠️ Testing limitation worth carrying into 2B

The whole suite runs on the **EF InMemory provider, which does not enforce unique indexes**.
That is how the soft-delete/unique-index conflict reached a running app with a green suite:
`RoleServiceSoftDeleteTests` asserted a deleted role's name is reusable and passed, while the
same operation returned **HTTP 500** against SQL Server. Fixed in `052254e` by filtering the
seven affected indexes on `[IsDeleted] = 0`. **Any uniqueness rule added in 2B (category
slugs, slider fields) must be checked against SQL Server by hand — a passing test proves
nothing here.**

### ⚠️ Second thing to carry into 2B: soft-deleting a principal

Soft delete is not free for anything that other rows point at. Two questions now have to be
answered for **every** delete path 2B adds — `Category` (children, products) and `Slider` both
qualify:

1. **Does it still throw?** Fixed globally by the deferred cascade timing above, and covered by
   `ProductSoftDeleteCleanupTests`. Nothing more to do unless a service loads dependents itself.
2. **What dangles afterwards?** This one is *per service* and has no global fix. The row survives
   with `IsDeleted = 1`, so every reference to it survives too. `ProductService.DeleteAsync` had
   to explicitly clear favourites and cart items while deliberately leaving order items alone.
   Deleting a category must make the same call about its child categories and its products.

---

## 3. Phase 2B — Categories, Clients & Sliders

Progress: **10 done · 0 remaining** — plan complete, closing checks all passed

| Task | Area | Deliverable | Status |
|---|---|---|---|
| 1 | Backend · Categories | Image upload + parent validation in `CategoryService` | ✅ Done — `6582396`, `CategoryServiceTests.cs` (7 tests); 83/83 backend tests pass |
| 2 | Backend · Categories | `AdminCategoriesController`; public `CategoriesController` → read-only | ✅ Done — `cd69b32`; manually verified `GET`/`POST`/admin-auth status codes; build clean, 83/83 tests pass |
| 3 | Frontend · Categories | Categories admin page (table + tree view) | ✅ Done — manually verified table/tree toggle, nested expand, image upload round-trip on create + update, and permission-gated read-only rendering (no Add/Actions when `categories.manage` is absent); `tsc --noEmit` clean |
| 4 | Backend · Clients | `IClientService` / `ClientService` over `ApplicationUser` | ✅ Done — `5d44ee8`, `ClientServiceTests.cs` (8 tests, real `UserManager` over in-memory context); 91/91 backend tests pass; not yet wired into DI or a controller (Task 5) |
| 5 | Backend · Clients | `AdminClientsController` | ✅ Done — `0db2f1e`; manually verified paged list, search, detail (`orderCount`/`lifetimeTotal`), 401 gate against the seeded dev DB. **Follow-up fix (`ef5600d`):** `api/Auth/login` didn't check Identity's lockout state, so disabling a client had no effect on their storefront login — `AuthService.GetTokenAsync` now calls `IsLockedOutAsync` and fails with the new `UserErrors.AccountLocked`. Verified end-to-end (disable → login rejected 400 → re-enable → login succeeds). 93/93 backend tests pass |
| 6 | Frontend · Clients | Clients admin page | ✅ Done — `660aa45`; manually verified search, detail card, edit (incl. email-change login proof), disable/enable, delete, and permission-gated read-only rendering. **Follow-up fix (same commit):** `.detail-grid dd` had no `overflow-wrap`, so a full email address overflowed its grid cell into the next column — added `overflow-wrap: break-word`. `tsc --noEmit` clean |
| 7 | Backend · Sliders | `Slider` entity, EF config, `sliders.view` permission, `AddSliders` migration | ✅ Done — `6682b95`; migration creates only `Sliders` (audit columns + 3 `Restrict` FKs to `Admins`), applied to the dev DB; `sliders.view` seeded onto Super Admin, confirmed idempotent on a second run; 95/95 backend tests pass |
| 8 | Backend · Sliders | `ISliderService` / `SliderService` | ✅ Done — `ca9fe16`, `SliderServiceTests.cs` (11 tests: upload, `ImageRequired`, `InvalidSchedule`, storage-failure propagation, image round-trip on update, active/schedule filtering, sort order, toggle, delete, not-found); 106/106 backend tests pass |
| 9 | Backend · Sliders | `AdminSlidersController` + public `SlidersController` | ✅ Done — `5600d9d`; manually verified create (with image upload) → admin + public lists → 401 gate → status toggle drops/restores it from the public list → expired `EndsOn` drops it too → uploaded file servable via `UseStaticFiles`; build clean, 106/106 tests pass |
| 10 | Frontend · Sliders | Sliders admin page | ✅ Done — `3439654`; manually verified no-image error, image upload + thumbnail, edit round-trip, `StartsOn`/`EndsOn` scheduling drops/restores from `api/Sliders`, `InvalidSchedule` inline error, toggle, delete, permission-gated read-only rendering; `tsc --noEmit` clean |

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

## 4. Phase 3 — Products Admin

Progress: **4 done · 0 remaining** — plan complete, final whole-branch review passed, merged to `main`

Executed via `superpowers:subagent-driven-development` (fresh implementer subagent per task, task-scoped review after each, final whole-branch review at the end).

| Task | Area | Deliverable | Status |
|---|---|---|---|
| 1 | Backend | `ProductService` image upload, `StockQuantity`/`Status`/`Feature` honored on create (previously hardcoded), admin search + pagination (`GetAdminPageAsync`), `ProductRequestValidator` | ✅ Done — `7d0a298`; 9 new tests; task review: approved clean |
| 2 | Backend | Product image gallery — `ProductImage` entity goes live (`GetAdminDetailAsync`/`AddImagesAsync`/`DeleteImageAsync`), ordered by `Sort` | ✅ Done — `770b803`; 7 new tests (16 total); task review: approved clean, verified genuine query-level ordering and cross-product delete isolation |
| 3 | Backend | `AdminProductsController` (full CRUD + gallery endpoints, `products.view`/`products.manage` gated); public `ProductsController` trimmed to `GET`/`GET {slug}`, fully unauthenticated | ✅ Done — `112eb96`; auth-test file rewritten wholesale (127 total tests); task review: approved clean, security-sensitive lockdown verified end-to-end |
| 4 | Frontend | Products admin page — search, pagination, create/edit form, cover image, gallery add/remove; nav + routes + SSR wiring | ✅ Done — `b9faaf3`, fix round `2f4788e` (Description data-loss race on quick Edit→Save, found in task review, fixed and re-verified clean) |

**Final whole-branch review** (most capable model, range `736bbba..2f4788e`): verdict "Ready to merge with fixes." Verified independently (not just trusted): the "no migration needed" claim (checked actual migration files), authorization traced end-to-end from frontend through controller to service with no gap, soft-delete on gallery images works via the global filter, storefront reads unchanged. Found 5 Important + 3 Minor issues invisible to any single task's diff (a stuck-Save UX dead-end on a failed detail fetch, an untested new validator, a missing category-existence check the plan wrongly claimed already existed, an undeclared `categories.view` dependency plus a `categoryId: 0` validator bug, and a null-`imageFiles` NRE risk on the gallery-upload endpoint) — all fixed in one consolidated commit (`0f772de`, 137/137 tests) and confirmed by a scoped re-review.

**Parked, non-blocking (ruling recorded, not fixed):** the new category-existence check is filtered by the global soft-delete filter, so editing a product whose category was later soft-deleted now fails (previously silent success) — this requires soft-deleting a category still referenced by products, an operational sequence `CategoryService.DeleteAsync` has never guarded against; it's a pre-existing category-lifecycle gap, not something Phase 3 introduced. Follow-up recommended: guard category soft-delete while referenced, or exempt unchanged-`CategoryId` updates from the existence check.

**Closing checks — all passed (2026-08-24)**
- ✅ `dotnet test backend/Ecommerce.Tests/Ecommerce.Tests.csproj` — 137/137 pass (on the worktree, and re-verified on the merged `main` result).
- ✅ `npx tsc --noEmit -p frontend/tsconfig.app.json` — 0 errors (worktree and merged result).
- ✅ Merged to `main` via clean fast-forward (`736bbba..0f772de`, 6 commits), pushed to `origin/main`.
- ⬜ `npm run build` from `frontend/` (production build + bundle-budget check) — not run this pass; worth doing before Phase 4 in case the new admin page tips the budget again (raised once already in Phase 2B).
- ⬜ Full manual browser walkthrough (admin login → Products CRUD → gallery → storefront `/products`/`/products/:slug` regression check) — not performed; verification for this phase relied on automated tests + two levels of code review rather than a manual pass. Worth doing opportunistically if touching this area again.

---

## 5. Phase 4 — Orders Admin

Progress: **3 done · 0 remaining** — plan complete, final whole-branch review passed, merged to `main`

Executed via `superpowers:subagent-driven-development`, same as Phase 3. This phase's SDD run was the most eventful yet — every one of the 3 tasks needed at least one fix round.

| Task | Area | Deliverable | Status |
|---|---|---|---|
| 1 | Backend | `OrderAdminService` — admin order list (search order#/name/email/mobile + status filter, paginated), detail, forward-only `OrderStatus` transitions + freely-editable `PaymentStatus`, restock-on-cancel, new `Order.StatusUpdatedOn` column | ✅ Done — `9d277a0`, fix round `433bc63` (see below); 18 new tests |
| 2 | Backend | `AdminOrdersController` at `api/Admin/Orders`, keyed by `orderNumber`, gated `orders.view`/`orders.manage` | ✅ Done — `efc4c73`; approved clean, no fix round; 4 new auth tests |
| 3 | Frontend | Orders admin page — search, status filter, pagination, detail panel with dual status editors | ✅ Done — `ebfdc3a`, fix round `173f9fa` (see below) |

**Task 1 fix round:** task review caught a real EF Core defect — `Include(x => x.User)` on `Order.User` (a required navigation whose target `ApplicationUser` carries the global `!IsDeleted` filter) silently drops the *entire* `Order` row, not just nulls the navigation, when the customer account is soft-deleted. Only `GetByOrderNumberAsync` had been patched; `GetAllAsync` (would have hidden such orders from the admin list entirely) and `UpdateStatusAsync` (would have falsely reported `OrderNotFound`) still carried it. Fixed across all three methods by avoiding the navigation entirely — batch-loading/looking up customer emails via separate queries and passing them as parameters rather than mutating `order.User`. Confirmed the underlying implementer report's test-evidence was unreliable this round (claimed "1 failed", the controller's own independent re-run showed 16/16 clean) — a reminder that implementer self-reports need independent verification, not just trust.

**Task 3 fix round:** task review caught a genuine async race in the frontend detail view — clicking **View** on order A then order B before A's fetch resolved let whichever response arrived last silently overwrite the detail panel, including discarding an in-progress status-dropdown selection; **Save** could then commit against the wrong order. Fixed with a request-token guard (only the latest `view()` call's response is ever applied).

**Final whole-branch review** (most capable model, range `86549e2..173f9fa`, then fix `5f065b0`): verdict "Ready to merge with fixes." Verified independently: 159/159 tests, 0 tsc errors, all 8 Global Constraints hold, the one write endpoint (`UpdateStatusAsync`) has no authorization gap for a direct API caller. Found 4 Important issues invisible to any single task's diff — no enum validation on `UpdateOrderStatusRequest` (an out-of-range value would pass the ordinal transition check and permanently brick an order, and corrupt the customer-facing tracking page), a `?? order.CreatedOn` fallback in the tracking-timeline fix that showed a *wrong* date for every pre-migration order (worse than the `null` it replaced), zero test coverage for `OrderService.BuildTracking`, and a page-wide error surface that structurally could never render (nested inside a conditional that was false exactly when there was an error) — all fixed in one consolidated commit (`5f065b0`, 165/165 tests) and confirmed by a scoped re-review.

**Parked, non-blocking (rulings recorded, not fixed):**
- Two trivial, self-correcting issues introduced by the final fix commit itself: a stale error banner can linger through one extra list reload before self-clearing; one code comment cross-references a method a same-commit DRY refactor moved code out of. Neither affects behavior or data — deemed not worth a fourth review round.
- Double-restock risk if two admins cancel the same order concurrently (no optimistic concurrency anywhere in the codebase — `OrderService.CreateAsync`'s stock decrement has the identical read-then-write shape). Explicitly out of scope for this branch; belongs in its own change if it's ever prioritized.

**Closing checks — all passed (2026-08-25)**
- ✅ `dotnet test backend/Ecommerce.Tests/Ecommerce.Tests.csproj` — 165/165 pass (worktree and re-verified on the merged `main` result).
- ✅ `npx tsc --noEmit -p frontend/tsconfig.app.json` — 0 errors (worktree and merged result).
- ✅ Merged to `main` via clean fast-forward (`86549e2..5f065b0`, 6 commits), pushed to `origin/main`.
- ⬜ `npm run build` from `frontend/` (production build + bundle-budget check) — not run this pass.
- ⬜ Full manual browser walkthrough — not performed; verification relied on automated tests + three levels of code review (per-task ×2, final whole-branch) rather than a manual pass.

---

## 6. Explicitly out of scope for Phase 2

- Products, Orders, Dashboard/Reports admin features (Phases 3–5).
- "View deleted / restore" UI for soft-deleted entities.
- Cloud/blob storage, image resizing, thumbnails, orphan-file cleanup.
- Storefront-facing slider carousel.
- Category drag-to-reorder and bulk operations.
- Admin editing of customer passwords, addresses, cards, or orders.

---

## 7. Phase 2B closing checks — all passed (2026-08-19)

- ✅ `dotnet test backend/Ecommerce.Tests/Ecommerce.Tests.csproj` — 106/106 pass.
- ✅ `dotnet build backend/Ecommerce.slnx` — 0 errors.
- ✅ `npx tsc --noEmit -p frontend/tsconfig.app.json` — 0 errors.
- ✅ `npm run build` from `frontend/` — completes (exit 0). **Follow-up fix (`41061cd`):**
  the three new admin pages pushed the initial bundle to 1.03 MB, past the pre-2B 1 MB
  hard-error budget — raised to 1.5 MB error / 800 kB warning in `angular.json`. Confirmed
  the prerender step does not attempt `/admin/categories`, `/admin/clients`, or
  `/admin/sliders` (all three stay `RenderMode.Client`).
- ✅ **Design-doc coverage sweep** — confirmed in the running app for all three modules
  (see each task's row above for the specific evidence): Categories' tree toggle + parent
  picker, Clients' lockout-based `isActive` + `SetEmailAsync`/`SetUserNameAsync`, Sliders'
  server-side schedule window on the public endpoint.
- ✅ **Manual walkthrough** — done per-module across Tasks 3/6/10 (create/edit/toggle/
  delete, plus permission-gated read-only rendering verified by stripping the `*.manage`
  claim from the stored session and confirming Add/Edit/Toggle/Delete disappear). Could
  **not** complete the literal "create a second admin, log in as them" variant — a newly
  created admin has no password field and requires the emailed reset-password link, and
  no SMTP is configured in this dev environment. The per-module localStorage-permission
  check exercises the same frontend `hasPermission()` gate a real second admin would hit,
  and the backend `[HasPermission]` gate was independently verified via curl (401/403) on
  every admin controller — but a real second-admin end-to-end login was not performed.
- ✅ **Storefront regression check** — `/home`, `/categories`, `/categories/1`, `/products`,
  `/cart`, `/checkout`, `/account`, `/account/orders` all load with zero browser console
  errors; login as the (re-seeded) `seed.tester@example.com` storefront customer works.
- ✅ No manual `IsDeleted`/`CreatedById`/`UpdatedById`/`DeletedById` assignment anywhere in
  `backend/Ecommerce/Services` or `backend/Ecommerce/Controllers` — confirmed by search.

**Next action (superseded 2026-08-25):** Phase 2B and Phase 3 are both merged into `main`.
Phase 4 — Orders admin (see §5) is now also done, merged, and pushed. **Phase 5 —
Dashboard/Reports** is the one remaining phase on the original roadmap; not yet designed,
needs its own spec + plan doc first, following the pattern of
`docs/superpowers/specs/2026-08-24-admin-phase4-orders-design.md` /
`docs/superpowers/plans/2026-08-25-admin-phase4-orders.md`.

Housekeeping note (from Phase 2B, still unresolved as of 2026-08-25): the dev database holds
two soft-deleted `Temp QA` roles (ids 3 and 5) left by the SQL-Server verification, and
`frontend/src/app/site/core/guards/auth-guard.ts` has an uncommitted edit on `main` that
predates this work and was never staged (still present, unrelated to Phases 3-4).

Housekeeping note (from Phase 4, new): the double-restock-under-concurrent-cancel risk in
`OrderAdminService.UpdateStatusAsync` (no optimistic concurrency on `Order`, matching
`OrderService.CreateAsync`'s existing stock-decrement pattern) was explicitly deferred —
worth a dedicated concurrency pass across all stock-mutating paths if it's ever prioritized,
not a one-off fix.
