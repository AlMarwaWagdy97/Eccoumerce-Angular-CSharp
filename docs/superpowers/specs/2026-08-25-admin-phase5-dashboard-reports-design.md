# Design: Admin Dashboard Phase 5 — Dashboard & Reports

**Date:** 2026-08-25
**Status:** Approved

## Goal

The last phase of the original admin roadmap. Today `/admin` (the dashboard
route) renders a pure stub — `DashboardComponent` is an empty class with a
static heading and a subtitle literally reading *"The full overview (sales,
orders, stock alerts) ships in a later phase."* This phase builds that
overview: KPI summary cards plus a small set of report widgets, backed by
real aggregation over the `Order`/`Product`/`ApplicationUser` data every
prior phase has been building up. `dashboard.view` and `reports.view` are
both already defined and seeded permissions (Phase 1) that have never gated
anything — this phase is the first to actually use either.

Two constraints shape this phase more than any prior one: there is **no
charting library** in the frontend (`package.json` confirmed — no Chart.js,
ngx-charts, ApexCharts, D3, or similar), so this is numeric KPI cards and
tables, not graphs; and dev data is thin (`DataSeeder.cs` seeds exactly 3
orders for one user), so every widget must degrade to a clear empty state
rather than assume rich data is present.

## Decisions (locked with user)

| Topic | Decision |
| --- | --- |
| One page, two permissions | A single admin page (replacing the `DashboardComponent` stub in place — same route, same nav entry) rather than a second "Reports" nav item/route. `dashboard.view` gates the page itself and its KPI/recent-orders section; `reports.view` gates a distinct report-widgets section within the same page. This reuses both existing, already-seeded permission keys meaningfully without adding a route the roadmap never called for as a separate item ("Dashboard / Reports" is one roadmap line, not two). |
| Backend split matches the frontend split | Two endpoints, not one: `GET api/Admin/Dashboard/summary` (`dashboard.view`) and `GET api/Admin/Dashboard/reports` (`reports.view`). A single combined endpoint would leak report data to a `dashboard.view`-only caller who hits the API directly — every prior phase's controllers enforce authorization server-side, not just via frontend hiding, and this phase keeps that invariant. |
| Fix the pre-existing missing guard | `app.routes.ts`'s dashboard child route (`{ path: '', component: DashboardComponent, title: 'Admin Dashboard' }`) is the **only** admin route without a `canActivate: [adminPermissionGuard(...)]` — confirmed by reading every other admin route, all seven of which have one. This phase adds `canActivate: [adminPermissionGuard('dashboard.view')]` to it. In scope because it's the exact permission this phase is building out; leaving it unguarded while shipping real dashboard data behind it would be inconsistent with every other page. |
| Revenue definition | Sum of `Order.Total` for every order **except `Cancelled`** (`Pending`/`Paid`/`Shipped`/`Delivered` all count) — a cancelled order was never fulfilled and restocked its items (Phase 4), so it shouldn't count as revenue. This is a "gross sales" figure, not "collected payments" (that would require filtering on `PaymentStatus == Paid` instead, which this phase does not attempt — see Out of Scope). |
| Low-stock threshold | A fixed constant, `LowStockThreshold = 5` (`Product.StockQuantity <= 5`), matching the codebase's existing convention of simple named constants for business thresholds (`ProductService.MaxPageSize`, `OrderService.FreeShippingThreshold`) rather than a configurable/admin-editable setting — YAGNI for a single dashboard widget. |
| No migration | Every field this phase needs already exists (`Order.Total`/`Status`/`CreatedOn`, `OrderItem.ProductTitle`/`LineTotal`/`ProductId`, `Product.StockQuantity`/`Status`, `ApplicationUser.CreatedOn`). No new index either — dev/demo data volume doesn't warrant one, and adding one speculatively for a data volume that doesn't exist yet is exactly what YAGNI argues against; revisit if this phase's queries are ever measured as slow against real production volume. |
| Graceful empty states | Every widget (KPI cards, recent orders, status breakdown, revenue-by-day, top products) must render a clear "nothing here yet" state when its underlying query returns zero rows, matching the `loading()`/`error()` signal pattern already used on every other admin page — not a blank space, not a spinner that never resolves. |

## `AdminDashboardController`

```csharp
[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Dashboard")]
[ApiController]
public class AdminDashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("summary")]
    [HasPermission(PermissionKeys.DashboardView)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)

    [HttpGet("reports")]
    [HasPermission(PermissionKeys.ReportsView)]
    public async Task<IActionResult> GetReportsAsync(CancellationToken cancellationToken)
}
```

Mirrors `AdminOrdersController`'s exact shape (class-level `[Authorize]`,
per-action `[HasPermission]`, `ApiResponse<T>` on every response). Both
`PermissionKeys.DashboardView`/`ReportsView` already exist and are already
seeded onto Super Admin (`AdminDataSeeder.cs`) — no permission-catalog
change needed.

## Contracts

```csharp
public record DashboardSummaryResponse(
    double TotalRevenue,
    int TotalOrders,
    int ActiveProductCount,
    int ClientCount,
    int LowStockProductCount,
    IReadOnlyList<RecentOrderResponse> RecentOrders);

public record RecentOrderResponse(
    string OrderNumber, string CustomerName, string Status, double Total, DateTime CreatedOn);

public record DashboardReportsResponse(
    IReadOnlyList<OrderStatusCountResponse> OrdersByStatus,
    IReadOnlyList<DailyRevenueResponse> RevenueByDay,
    IReadOnlyList<TopProductResponse> TopProducts);

public record OrderStatusCountResponse(string Status, int Count);

public record DailyRevenueResponse(DateOnly Date, int OrderCount, double Revenue);

public record TopProductResponse(long ProductId, string ProductTitle, int QuantitySold, double Revenue);
```

`RecentOrderResponse.CustomerName` reuses `Order.ShipToName` (the
already-established snapshot-field pattern from Phase 4's
`AdminOrderSummaryResponse` — no join to `ApplicationUser` needed, and
avoids Phase 4's own hard-won lesson about `Include()` on `Order.User`, a
required navigation filtered by soft-delete).

## `DashboardService`

```csharp
public interface IDashboardService
{
    Task<Result<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<Result<DashboardReportsResponse>> GetReportsAsync(CancellationToken cancellationToken = default);
}
```

- `GetSummaryAsync`: five independent aggregate queries (`Orders.Where(status
  != Cancelled).SumAsync(Total)`, `Orders.CountAsync()`, `Products.Where(Status).CountAsync()`,
  `Users.CountAsync()`, `Products.Where(StockQuantity <= LowStockThreshold).CountAsync()`)
  plus a sixth query for the 5 most recent orders (`OrderByDescending(CreatedOn).Take(5)`,
  mapped via `ShipToName`, no `Include(x => x.User)` — same defensive
  pattern Phase 4 landed on). Never fails on empty data — every aggregate
  naturally returns `0`/empty for a fresh database, so this method has no
  failure path and returns `Result<T>` only for interface consistency with
  every other service in this codebase (never `Result.Failure`).
- `GetReportsAsync`: three independent aggregate queries. Order-status
  breakdown groups all non-deleted orders by `Status` (all 5 possible
  values, `0` for statuses with no orders — not just the statuses present).
  Revenue-by-day groups non-cancelled orders from the last 7 days by
  `CreatedOn.Date`, filling in `0`/empty for days with no orders (7 rows
  always returned, oldest first). Top products groups `OrderItem` by
  `ProductId`/`ProductTitle` across non-cancelled orders, ordered by summed
  `LineTotal` descending, `Take(5)`. Also never fails.

Both methods use `AsNoTracking()` throughout (read-only aggregation, same
convention as every other admin list/report query in this codebase).

## Frontend

Replace `admin/features/pages/dashboard/dashboard.ts`'s stub in place (same
file paths, no new route):

- **KPI cards** (`dashboard.view`, always shown once loaded): total
  revenue, total orders, active products, clients, low-stock count — five
  simple cards, no chart. Below them, a **recent orders** mini-table (order
  number, customer, status pill, total, date) with a link to the full
  `/admin/orders` page (reusing the existing pill styling convention from
  the Orders/Products admin pages).
- **Reports section** (`reports.view`, only rendered if
  `auth.hasPermission('reports.view')` — the frontend gate matches the
  backend's separate-endpoint enforcement, it doesn't replace it): order
  status breakdown (a small table, 5 rows, one per `OrderStatus`), revenue
  by day (a 7-row table, oldest to newest), top 5 products by revenue (a
  table: product, quantity sold, revenue).
- Two independent `DashboardServices` calls (`getSummary()`,
  `getReports()`), each with its own `loading()`/`error()` signal pair, so
  a `reports.view`-less admin's page doesn't wait on or fail because of a
  call it never makes. `getReports()` is only invoked when
  `auth.hasPermission('reports.view')` is true — no doomed 403 request
  fired for admins who lack it.
- Each of the eight logical widgets (5 KPI cards + recent orders + 3 report
  tables) renders its own "Nothing here yet" message when its underlying
  array/count is empty, matching the Decisions-table requirement.
- `app.routes.ts`: add `canActivate: [adminPermissionGuard('dashboard.view')]`
  to the existing `{ path: '', component: DashboardComponent, ... }` route
  — the one line fixing the pre-existing gap. No new import, no new route
  entry, no `app.routes.server.ts` change (the route already exists and
  already has whatever render-mode entry it needs, unaffected by this
  phase — confirmed the dashboard path was never listed as `Client` because
  it never needed one; it still doesn't, since KPI data loads client-side
  after render like every other admin page, not via SSR-prerendered route
  params).
- No nav-entry change (`main-layout.ts`'s `NAV_ITEMS` already has
  `{ label: 'Dashboard', path: '.', ... }` — Phase 1, unchanged).

## Out of scope for this phase

- Charts/graphs of any kind — no charting library exists in the frontend
  and adding one is a dependency decision bigger than this phase's data
  (tables convey the same 7 rows/5 rows just as well at this scale).
- Date-range pickers or configurable report windows — "last 7 days" and
  "top 5" are fixed, matching the fixed page-size defaults used elsewhere
  (`pageSize = 20`) rather than exposing every number as a control.
  Revisit if real usage shows a fixed window is too limiting.
- CSV/Excel export of any report data.
- `PaymentStatus`-aware revenue (e.g. "collected" vs "pending" cash) — see
  the Decisions table; a single "gross" revenue figure is what this phase
  ships.
- Any change to `OrderAdminService`, `AdminOrdersController`, or the
  existing Orders/Products/Clients/Sliders/Categories admin pages — this
  phase only reads their underlying tables, it doesn't touch their code.
- A configurable low-stock threshold (admin-editable) — fixed constant per
  the Decisions table.
