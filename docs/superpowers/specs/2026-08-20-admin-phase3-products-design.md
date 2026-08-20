# Design: Admin Dashboard Phase 3 — Products

**Date:** 2026-08-20
**Status:** Approved

## Goal

Phase 2 (`docs/superpowers/specs/2026-08-12-admin-phase2-foundations-categories-clients-sliders-design.md`)
deferred Products deliberately: *"Products keeps URL strings until its own
phase."* This phase closes that gap — full admin management of the product
catalog: CRUD, a real multi-image gallery, and stock management. It's the
second of the three phases Phase 1's original roadmap left after Categories/
Clients/Sliders (Orders and Dashboard/Reports remain).

Two small bugs in the existing `ProductsController` were found and fixed in
a prior session while seeding dummy product data (commit `8fcf587`): a
broken `CreatedAtAction` route reference that made every `POST` return 500
despite the product actually being created, and `ProductResponse`/`Update`
not returning the saved data. Noted here for context — this phase's
`AdminProductsController` inherits those fixes rather than re-introducing
them.

## Decisions (locked with user)

| Topic | Decision |
| --- | --- |
| Admin vs. public controller | Same split as Categories/Clients/Sliders: new `AdminProductsController` at `api/Admin/Products` owns all writes and gated admin reads; public `ProductsController` trims to `GET ""`/`GET "{slug}"` only. |
| Images | **Multiple images per product**, via the existing (currently write-dead) `ProductImage` gallery entity. A distinct single **cover image** (`Product.Image`) stays the primary thumbnail everywhere (admin table, storefront cards, cart, checkout); the gallery is additional photos shown on the product detail page. |
| Gallery management | Add one or more images at a time; delete one at a time. **No reorder endpoint** — new images append in upload order via an incrementing `Sort` (YAGNI; can follow later if it turns out to matter). |
| Stock | `StockQuantity` becomes an editable admin field — it exists as a column today but `ProductRequest` never exposed it, so admins have never been able to set it. |
| Status/Feature on create | `AddAsync` currently **hardcodes** `Status = true, Feature = false` regardless of what's posted — same defect Categories Task 1 fixed. `ProductRequest` gains `Status`/`Feature`, honoured on both create and update. |
| Product list (admin) | Search + pagination, same shape as `ClientService.GetAllAsync(search, page, pageSize)` — the catalog is the one entity here expected to actually grow past a single page. |
| Request validation | `ProductRequestValidation` today validates the **entity** `Product`, not the DTO — dead code, nothing binds a raw `Product`, same situation Categories Task 1 flagged and left alone. This phase adds a proper `ProductRequestValidator : AbstractValidator<ProductRequest>` (mirrors `CategoryRequestValidator`/`SliderRequestValidator`) so admin input is actually validated for the first time. |
| Migration | **None.** `ProductImage` already inherits `AuditableEntity` (stamping/soft-delete come free from the `SaveChanges` hook, same as every other Phase 2B entity), and `Product.StockQuantity` is already a column — `ProductRequest` just never exposed it. |

## `AdminProductsController` + public `ProductsController`

Mirrors `AdminCategoriesController` exactly:

- `GET ""`, `GET "{id:long}"` — `[HasPermission(PermissionKeys.ProductsView)]`
- `POST ""`, `PUT "{id:long}"`, `DELETE "{id:long}"`, `PUT "{id:long}/toggleStatus"` — `[HasPermission(PermissionKeys.ProductsManage)]`
- `POST "{id:long}/images"`, `DELETE "{id:long}/images/{imageId:long}"` — gallery management, also `ProductsManage`

Both permission keys already exist (`PermissionKeys.ProductsView`/`ProductsManage`,
seeded since Phase 1) but `ProductsView` has never gated anything — admin
reads were simply public. This phase is the first to actually use it.

The admin list endpoint returns a paged response:

```csharp
public record ProductsPageResponse(
    IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);
```

— same shape as `ClientsPageResponse`, filtering `Title`/`Sku` (not `Slug`,
which is an implementation detail, not something an admin searches by).

The public `ProductsController` keeps only its two read actions
(unauthenticated, behaviour unchanged) and **loses its write actions** —
already gated behind `products.manage` since Phase 2A, but they physically
lived on the public controller until this phase moves them.

## Product images: cover + gallery

**Cover image** — identical pattern to Category/Slider: `ProductRequest`
gains `IFormFile? ImageFile` alongside the existing `string? Image`; if
present it's saved via `IFileStorage` (module `"products"`) and its path
wins, otherwise `Image` is kept as-is (or replaced if a new string was
posted). No change to the `Product.Image` column.

**Gallery** — the *read* side already exists and has been silently returning
empty lists since Phase 1: `ProductService.GetByIdOrSlugAsync` already
projects `product.Images.Select(i => i.Url)` into the public
`ProductDetailsResponse.Images`, ordered by `Sort` — it's just that nothing
has ever inserted a `ProductImage` row, so every product's gallery has always
been empty. This phase adds the *write* side only:

```csharp
public record ProductImageResponse(long Id, string Url, int Sort);

Task<Result<IReadOnlyList<ProductImageResponse>>> AddImagesAsync(
    long productId, IReadOnlyList<IFormFile> files, CancellationToken ct = default);
Task<Result> DeleteImageAsync(long productId, long imageId, CancellationToken ct = default);
```

`AddImagesAsync` saves each file via `IFileStorage` (module `"products"`,
same as the cover) and inserts a `ProductImage` row per file with
`Sort = current max + 1`. `DeleteImageAsync` is a plain `Remove()` — the
`SaveChanges` hook turns it into a soft delete, consistent with everything
else, and the global `!IsDeleted` filter already keeps
`GetByIdOrSlugAsync`'s `Include(p => p.Images...)` correct with zero extra
code.

Gallery uploads are a **separate step from create** — `POST api/Admin/Products`
takes only the cover image; gallery photos get added afterward via
`POST api/Admin/Products/{id}/images`, once the product (and its id) exists.
This keeps the create form's multipart shape identical to Category/Slider
and avoids a two-different-file-fields-in-one-request format.

`AdminProductDetailResponse.Images` is ordered by `Sort` ascending, same as
the public detail response's `Include(p => p.Images.OrderBy(i => i.Sort))`.

Because the admin edit view needs to delete individual gallery images by id,
it needs a response shape the public one doesn't provide (structured
`Id`/`Sort`, not bare URL strings). Rather than change the public
`ProductDetailsResponse.Images` contract (the storefront already consumes it
as `string[]` — see `product-details.ts`'s `gallery` computed), this phase
adds a distinct `AdminProductDetailResponse` for `GET api/Admin/Products/{id}`
with `IReadOnlyList<ProductImageResponse> Images`. The public detail
response is untouched.

## Stock management

`ProductRequest` gains `int? StockQuantity`, honoured on create (default 0
if omitted — matches today's implicit behaviour) and update. No change to
how `OrderService` decrements it at checkout, and no change to
`DataSeeder`'s blanket "top up anything ≤ 0" replenishment — that stays as
dev-only convenience, unrelated to admin-set stock.

## Frontend

New `admin/features/pages/products/`, following the Clients page shape (it's
the other module with search + pagination):

- **Table**: cover thumbnail, title, SKU, category, price, stock, status,
  actions. Search box (title/SKU) + pager, same as Clients.
- **Form**: title, slug, SKU, category (`<select>`, populated from
  `CategoryServices.getCategories()` — already exists), price,
  price-after-sale, sale %, sort, stock quantity, description, meta
  description/key, status, feature, cover image picker.
- **Gallery section**, shown only once a product exists (i.e. in the edit
  view, not on initial create — matches the backend's two-step upload):
  existing images as a thumbnail strip with a per-image delete button, plus
  an "add images" multi-file picker appending more.
- Nav entry (`Products`, after `Sliders`), route
  `canActivate: [adminPermissionGuard('products.view')]`,
  `RenderMode.Client` server-route entry — same wiring as every prior
  module. This is the only module in this phase, so none of Phase 2B's
  three-way file-conflict sequencing constraint applies.

## Out of scope for this phase

- **Orders** and **Dashboard/Reports** admin features — the two remaining
  phases.
- Gallery drag-to-reorder UI (`Sort` exists; no reordering UI, matching the
  Category precedent of "field exists, no reorder UI").
- Bulk product operations (bulk import/export, bulk price edit).
- Product review moderation — the `Review` entity already exists and is
  populated by customers; admin moderation/removal of reviews is not part of
  this phase.
- Any change to the storefront's existing gallery/thumbnail-row UI in
  `product-details.html` — it already renders `p.images`; this phase's only
  effect there is that the array is no longer always empty.
- Cloud/blob storage, image resizing, thumbnails, orphan-file cleanup — same
  standing exclusion as Phase 2.
