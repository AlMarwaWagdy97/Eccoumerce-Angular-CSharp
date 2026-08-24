# Design: Admin Dashboard Phase 4 — Orders

**Date:** 2026-08-24
**Status:** Approved

## Goal

Phase 3 (`docs/superpowers/specs/2026-08-20-admin-phase3-products-design.md`)
left Orders and Dashboard/Reports as the two remaining phases of the original
admin roadmap. This phase closes Orders: today `orders.view`/`orders.manage`
permissions are already seeded (Phase 1) and completely unused, the customer-
facing `OrdersController`/`OrderService` are 100% self-service (every query
hard-filters by the caller's `UserId`, with no list-all/status-change
capability anywhere), and **no code path in the entire codebase ever
transitions an order's status after creation** — every order is created
`Pending` and stays there forever unless a developer hand-edits the database.
This phase adds real admin order management: list, detail, and status
updates, on a clean slate (there is no existing dead stub or partial admin
code for Orders to build on, unlike Phase 3's Products).

## Decisions (locked with user)

| Topic | Decision |
| --- | --- |
| Service split | New `IOrderAdminService`/`OrderAdminService`, separate from the existing customer-scoped `IOrderService`/`OrderService`. Every method on the existing service filters by `UserId` inline in its query — there is no unscoped "get by id" to reuse, and admin queries must never be accidentally user-scoped. Keeping them separate also means this phase cannot regress the customer-facing account/tracking pages, which stay completely untouched. |
| Controller + route key | New `AdminOrdersController` at `api/Admin/Orders`, same `[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]` + `[HasPermission(...)]` shape as `AdminProductsController`/`AdminClientsController`. **Keyed by `orderNumber` (string), not numeric `id`** — this is a deliberate difference from Products/Clients: `OrderNumber` is already the order's external-facing identifier everywhere else in the codebase (the existing customer `OrdersController` routes on it, e.g. `GET api/Orders/{orderNumber}`), so the admin routes reuse the same key rather than introducing a second identifier. |
| Scope of admin actions | View (list + detail) and status updates (both `OrderStatus` and `PaymentStatus` — see below) only. No editing of line items, shipping address, or amounts; no refunds/actual payment processing; no customer-initiated cancellation (unrelated, not part of this phase). |
| Status transitions | `OrderStatus` is the ordinal-ordered enum `Pending(0) < Paid(1) < Shipped(2) < Delivered(3)`, plus terminal `Cancelled(4)`. A transition is legal only if: the new status is strictly greater (forward-only, no reverting Shipped back to Paid) among `Pending/Paid/Shipped/Delivered`, **or** the new status is `Cancelled` and the current status is not already `Delivered` or `Cancelled` (both terminal). Setting the same status again is a no-op success (idempotent PUT), not an error. Every other transition fails with a new `OrderErrors.InvalidStatusTransition`. |
| PaymentStatus | **Editable, alongside OrderStatus, in the same update.** `UpdateOrderStatusRequest` carries both fields; admin can change either or both in one `PUT`. Unlike `OrderStatus`, `PaymentStatus` (`Pending`/`Paid`/`Failed`) has **no forward-only transition rule** — a payment can legitimately move in either direction in the real world (mark paid on cash collection, mark failed on a later chargeback, mark paid again on a retried/corrected payment), so any value is accepted at any time, independent of whether the order itself is in a terminal `OrderStatus`. `StatusUpdatedOn` (see below) tracks `OrderStatus` changes only, not `PaymentStatus` changes. |
| Cancel restocks inventory | Cancelling an order (`OrderStatus` transitioning to `Cancelled`) adds each `OrderItem.Quantity` back onto its `Product.StockQuantity` — order creation decrements stock and nothing today ever restores it, so every cancellation currently leaks inventory permanently. The restock lookup uses `IgnoreQueryFilters()` on the product query specifically so cancelling an order whose product was later soft-deleted still restocks correctly instead of silently no-oping (the same soft-delete/referenced-row interaction flagged as a parked risk in Phase 3's final review). |
| Status timestamp | New nullable `Order.StatusUpdatedOn` (`DateTime?`) column, set to "now" every time `UpdateStatusAsync` performs a real (non-no-op) transition. Fixes a real, currently-visible gap: the customer-facing tracking page's `BuildTracking()` has never had a real completion date for anything past the `Pending` step (`OrderService.cs:152-181` hardcodes `CompletedOn = null` for every later step) — once this column exists, `BuildTracking` is updated to use it for the current step's `CompletedOn`, so tracking becomes accurate going forward. **No backfill**: existing rows get `StatusUpdatedOn = null` and the tracking page's existing null-safe rendering already handles that (nothing to migrate defensively; the column only starts getting real values from the first admin-driven status change onward). |
| List filters | Free-text `search` matches **order number, customer name, email, or mobile** (any one field containing the term — a single search box, not four separate inputs), plus a `status` dropdown filter (`OrderStatus`). `OrderNumber`, `ShipToName` (name), and `ShipToPhone` (mobile) are matched directly on `Order` — no join needed, both are already-stored snapshot fields. Email match requires the existing `Order.User` navigation (`.Where(o => ... || o.User.Email.Contains(term))`), which EF Core translates into a single SQL join; this is a read, not a new persisted relationship. |
| Customer display | List and detail responses include `CustomerName` (from `Order.ShipToName`, no join) and `CustomerEmail`/`CustomerMobile` (from `Order.User.Email` / `Order.ShipToPhone`) for display. |
| Migration | One: add nullable `Order.StatusUpdatedOn` (`DateTime?`). No other schema changes — `Order`/`OrderItem` are already `AuditableEntity` (stamping/soft-delete come free from the `SaveChanges` hook), and every other field this phase needs already exists. |

## `AdminOrdersController`

```csharp
[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Orders")]
[ApiController]
public class AdminOrdersController(IOrderAdminService orderAdminService) : ControllerBase
{
    [HttpGet("")]
    [HasPermission(PermissionKeys.OrdersView)]
    // GET api/Admin/Orders?search=&status=&page=1&pageSize=20
    public async Task<IActionResult> GetAllAsync(...)

    [HttpGet("{orderNumber}")]
    [HasPermission(PermissionKeys.OrdersView)]
    public async Task<IActionResult> GetByOrderNumberAsync([FromRoute] string orderNumber, ...)

    [HttpPut("{orderNumber}/status")]
    [HasPermission(PermissionKeys.OrdersManage)]
    // Body carries both OrderStatus and PaymentStatus — one endpoint updates either or both.
    public async Task<IActionResult> UpdateStatusAsync([FromRoute] string orderNumber, [FromBody] UpdateOrderStatusRequest request, ...)
}
```

`PermissionKeys.OrdersView`/`OrdersManage` already exist and are already
seeded onto the Super Admin role (`AdminDataSeeder.cs`) — this phase is the
first to actually gate anything with them, exactly like `ProductsView` was
in Phase 3.

## Contracts

```csharp
public record AdminOrderSummaryResponse(
    long Id, string OrderNumber, string CustomerName, string CustomerEmail, string CustomerMobile,
    string Status, string PaymentStatus, double Total, DateTime CreatedOn);

public record OrdersPageResponse(
    IReadOnlyList<AdminOrderSummaryResponse> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record AdminOrderDetailResponse(
    long Id, string OrderNumber, string CustomerName, string CustomerEmail, string CustomerMobile,
    string Status, string PaymentMethod, string PaymentStatus,
    double SubTotal, double ShippingCost, double Total,
    string ShipToName, string ShipToPhone, string ShipToLine1, string? ShipToLine2,
    string ShipToCity, string ShipToState, string ShipToCountry, string? ShipToPostalCode,
    DateTime CreatedOn, DateTime? StatusUpdatedOn,
    IReadOnlyList<OrderItemResponse> Items);

// Both fields are always required — the admin UI's two dropdowns (Order
// Status, Payment Status) always submit their current selection together,
// so there is no ambiguity about "unspecified means leave unchanged."
public record UpdateOrderStatusRequest(OrderStatus Status, PaymentStatus PaymentStatus);
```

`OrderItemResponse` is reused as-is from the existing
`Contracts/Orders/OrderItemResponse.cs` (`ProductId, ProductTitle,
ProductImage, UnitPrice, Quantity, LineTotal`) — no change needed, the
customer-facing and admin detail views show the same line-item shape.

`OrdersPageResponse` mirrors `ProductsPageResponse`/`ClientsPageResponse`'s
five-field shape exactly.

## `OrderAdminService`

```csharp
public interface IOrderAdminService
{
    Task<Result<OrdersPageResponse>> GetAllAsync(string? search, OrderStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<Result<AdminOrderDetailResponse>> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<Result<AdminOrderDetailResponse>> UpdateStatusAsync(string orderNumber, OrderStatus newStatus, PaymentStatus newPaymentStatus, CancellationToken ct = default);
}
```

- `GetAllAsync`: unscoped (no `UserId` filter — this is the entire point of
  the admin service). `search`, when supplied, matches `OrderNumber`,
  `ShipToName`, `ShipToPhone`, or `User.Email` (case-insensitive `Contains`
  against any one of the four — one free-text box, not four separate
  filters); `status`, when supplied, is an exact `OrderStatus` filter.
  Requires `Include(o => o.User)` for the email match/projection. Ordered by
  `CreatedOn` descending (most recent orders first — this is the one list in
  the admin panel where recency, not alphabetical order, is what an admin
  actually wants).
- `GetByOrderNumberAsync`: `Include(o => o.User).Include(o => o.Items)`,
  no `UserId` filter, 404 (`OrderErrors.OrderNotFound`) if the order number
  doesn't exist.
- `UpdateStatusAsync`: loads the order (same include shape as
  `GetByOrderNumberAsync`). Handles the two fields independently:
  - **`OrderStatus`**: validated per the transition rule in the Decisions
    table; a no-op (no mutation, no `StatusUpdatedOn` bump) if
    `newStatus == order.Status`; an illegal transition fails the whole call
    with `OrderErrors.InvalidStatusTransition` **before any field is
    written** (including `PaymentStatus` — the update is all-or-nothing, an
    admin never ends up with `PaymentStatus` changed but `OrderStatus`
    silently rejected). If the (validated) new status is `Cancelled`,
    restocks every line item's `Quantity` onto its `Product` (queried with
    `IgnoreQueryFilters()` so a soft-deleted product still gets its stock
    corrected) before saving. Otherwise sets `order.Status = newStatus` and
    `order.StatusUpdatedOn = <now>`.
  - **`PaymentStatus`**: no transition validation — `order.PaymentStatus =
    newPaymentStatus` unconditionally (including a no-op if it's already
    that value; cheap enough not to special-case).
  - Saves once, returns the updated detail response.

## Migration

```csharp
migrationBuilder.AddColumn<DateTime>(
    name: "StatusUpdatedOn",
    table: "Orders",
    type: "datetime2",
    nullable: true);
```

No index needed (never filtered or sorted on). No backfill (see the
Decisions table).

## Customer-facing tracking fix (small, in-scope side effect)

`OrderService.BuildTracking()` (`OrderService.cs:152-181`) currently
hardcodes every step's `CompletedOn` to `null` except `Pending` (which uses
`order.CreatedOn`). Once `StatusUpdatedOn` exists, the step matching the
order's **current** status uses `order.StatusUpdatedOn ?? order.CreatedOn`
for its `CompletedOn` instead of always `null` — every step strictly before
the current one keeps showing `null` (this phase does not add a full
per-transition history table; only "when did it reach its current status"
becomes accurate, not "when did it pass through each prior status"). This is
a one-line-per-branch change to existing, already-tested code, not a new
feature — flagged here so the plan accounts for it and the reviewer doesn't
read it as scope creep.

## Frontend

New `admin/features/pages/orders/`, following the Clients/Products page
shape (search + pagination + a detail view):

- **Table**: order number, customer (name + email + mobile), total, order
  status pill + payment status pill (color-coded per status, same
  `pill`/`pill-off`-style convention as Products), date. One search box
  (matches order number/name/email/mobile server-side) + `OrderStatus`
  `<select>` filter + pager.
- **Detail panel**: opened by clicking a row (no separate route — same
  in-page show/hide pattern Products/Categories use for their edit form,
  not a navigation). Shows the full shipping address, every line item
  (product title, sku is not needed here since it's not admin-editable,
  unit price, quantity, line total), and **two independent `<select>`
  controls** gated behind `orders.manage`: an Order Status dropdown
  (disabled entirely when the order is already `Delivered` or `Cancelled`,
  both terminal; otherwise only offers legal forward transitions plus
  `Cancelled`, matching the backend's own validation so the UI never lets an
  admin attempt an update the API would reject) and a Payment Status
  dropdown (`Pending`/`Paid`/`Failed`, always enabled — no transition
  restriction, matching the backend). One Save button submits both fields
  together via the single `PUT {orderNumber}/status` call.
- Nav entry: `{ label: 'Orders', path: 'orders', icon: 'bi-receipt', permission: 'orders.view' }`,
  placed after **Sliders** and before **Roles** — grouping catalog/people/
  marketing (Categories, Products, Clients, Sliders) together, with the
  transactional Orders view immediately after, ahead of the system-admin
  section (Roles, Admins).
- Route `canActivate: [adminPermissionGuard('orders.view')]`,
  `RenderMode.Client` server-route entry — same wiring as every prior
  module. This is the only module in this phase, so Phase 2B's three-way
  file-conflict sequencing constraint (Tasks editing `app.routes.ts` /
  `app.routes.server.ts` / `main-layout.ts` in parallel) does not apply.
- New `admin/core/services/order-services.ts` / `admin/shared/interface/orderInterface.ts`,
  following `product-services.ts`/`productInterface.ts`'s conventions
  (`HttpParams` for search/status/page/pageSize, `AdminApiEnvelope<T>`
  unwrap). No multipart handling needed anywhere in this feature — nothing
  here uploads a file.

## Out of scope for this phase

- **Dashboard/Reports** — the one remaining phase after this.
- Refunds and real payment processing (`PaymentStatus` editing itself is
  in scope — see Decisions — but no actual money moves; it's a manual
  record-keeping field, same as it is today).
- Customer-initiated order cancellation.
- Editing an order's line items, shipping address, or amounts after creation.
- A full per-transition status history/audit log (only "current status's
  timestamp" is added, not a table of every past transition).
- Any change to `CheckoutComponent`/`OrderService.CreateAsync` — order
  creation behavior is completely unchanged by this phase.
- Bulk order operations (bulk status update, export).
