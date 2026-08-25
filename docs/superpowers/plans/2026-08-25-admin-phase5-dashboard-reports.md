# Admin Phase 5 — Dashboard & Reports Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the `DashboardComponent` stub (a static heading and a subtitle literally reading "the full overview ships in a later phase") with a real admin dashboard: KPI summary cards, a recent-orders list, and a reports section (order-status breakdown, revenue by day, top products). This is the last phase on the original admin roadmap.

**Architecture:** New `IDashboardService`/`DashboardService` aggregating over the existing `Order`/`OrderItem`/`Product`/`ApplicationUser` tables (no new entities, no migration). New `AdminDashboardController` at `api/Admin/Dashboard` with two endpoints — `summary` (gated `dashboard.view`) and `reports` (gated `reports.view`) — kept separate so a `dashboard.view`-only caller can never pull report data by hitting the API directly. Frontend replaces the stub component in place; no new route.

**Tech Stack:** ASP.NET Core 10 / EF Core (backend), Angular 22 standalone components + signals (frontend) — same stack as every prior phase. No charting library (none exists in the frontend) — KPI cards and tables only.

**Spec:** `docs/superpowers/specs/2026-08-25-admin-phase5-dashboard-reports-design.md`

## Global Constraints

- No EF migration — every field this phase needs (`Order.Total`/`Status`/`CreatedOn`, `OrderItem.ProductTitle`/`Quantity`/`LineTotal`, `Product.StockQuantity`/`Status`, `ApplicationUser.CreatedOn` and its row count) already exists.
- No service takes an `adminId` parameter — this phase's methods are pure reads with no writes at all, so this doesn't even arise, but no method should take one regardless, matching every other admin-managed feature.
- "Revenue" always means the sum of `Order.Total` for every status **except `Cancelled`** — never all orders unconditionally.
- The low-stock threshold is a fixed constant (`<= 5`), not admin-configurable.
- `GetReportsAsync`'s "revenue by day" window is always exactly 7 rows (today and the 6 days before it), oldest first, with `0`/empty for any day with no qualifying orders — never fewer than 7 rows, never omitting a zero day.
- `GetReportsAsync`'s "orders by status" always returns exactly 5 rows (one per `OrderStatus` value), `0` for any status with no orders — never omitting a status that happens to have zero orders.
- Neither `GetSummaryAsync` nor `GetReportsAsync` ever returns `Result.Failure` — every aggregate naturally resolves to `0`/empty on a fresh or sparse database, so there is no failure path to model.
- No `Include()` on a required, soft-delete-filtered navigation (the exact defect Phase 4 found and fixed in `OrderAdminService`) — any query that needs to know whether an `Order` is cancelled while operating on `OrderItem` rows must filter by `Order.Id` via a subquery, never by accessing `OrderItem.Order.Status` directly in a predicate.

---

### Task 1: `IDashboardService`/`DashboardService`

**Files:**
- Create: `backend/Ecommerce/Contracts/Dashboard/DashboardResponse.cs`
- Create: `backend/Ecommerce/Services/IDashboardService.cs`
- Create: `backend/Ecommerce/Services/DashboardService.cs`
- Modify: `backend/Ecommerce/DependacyInjection.cs`
- Test: `backend/Ecommerce.Tests/Services/DashboardServiceTests.cs`

**Interfaces:**
- Consumes: `ApplicationDbContext.Orders`/`OrderItems`/`Products`/`Users` (existing `DbSet`s, already filtered by the global `!IsDeleted` query filter on every `IAuditable` type); `Order.ShipToName`/`Total`/`Status`/`CreatedOn`; `OrderItem.ProductId`/`ProductTitle`/`Quantity`/`LineTotal`; `Product.StockQuantity`/`Status`; `OrderStatus` enum (existing).
- Produces: `DashboardSummaryResponse`, `RecentOrderResponse`, `DashboardReportsResponse`, `OrderStatusCountResponse`, `DailyRevenueResponse`, `TopProductResponse` — Task 2's `AdminDashboardController` consumes all six. `IDashboardService.GetSummaryAsync(CancellationToken)` / `GetReportsAsync(CancellationToken)` — Task 2's controller calls both.

- [ ] **Step 1: Write the contracts**

```csharp
// backend/Ecommerce/Contracts/Dashboard/DashboardResponse.cs
namespace Ecommerce.Contracts.Dashboard;

public record DashboardSummaryResponse(
    double TotalRevenue,
    int TotalOrders,
    int ActiveProductCount,
    int ClientCount,
    int LowStockProductCount,
    IReadOnlyList<RecentOrderResponse> RecentOrders);

public record RecentOrderResponse(
    string OrderNumber,
    string CustomerName,
    string Status,
    double Total,
    DateTime CreatedOn);

public record DashboardReportsResponse(
    IReadOnlyList<OrderStatusCountResponse> OrdersByStatus,
    IReadOnlyList<DailyRevenueResponse> RevenueByDay,
    IReadOnlyList<TopProductResponse> TopProducts);

public record OrderStatusCountResponse(string Status, int Count);

public record DailyRevenueResponse(DateOnly Date, int OrderCount, double Revenue);

public record TopProductResponse(long ProductId, string ProductTitle, int QuantitySold, double Revenue);
```

- [ ] **Step 2: Write the service interface**

```csharp
// backend/Ecommerce/Services/IDashboardService.cs
using Ecommerce.Contracts.Dashboard;

namespace Ecommerce.Services;

public interface IDashboardService
{
    Task<Result<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<Result<DashboardReportsResponse>> GetReportsAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Write the failing tests**

```csharp
// backend/Ecommerce.Tests/Services/DashboardServiceTests.cs
using Ecommerce.Entities;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class DashboardServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<ApplicationUser> SeedUserAsync(ApplicationDbContext context, string email = "buyer@example.com")
    {
        var user = new ApplicationUser { UserName = email, Email = email, FirstName = "Bea", LastName = "Buyer" };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<long> SeedCategoryAsync(ApplicationDbContext context)
    {
        var category = new Category { Title = "Shoes", Slug = "shoes" };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<Product> SeedProductAsync(
        ApplicationDbContext context, long categoryId, string title = "Runner", string sku = "SKU-1",
        int stockQuantity = 10, bool status = true)
    {
        var product = new Product
        {
            Title = title,
            Slug = title.ToLower(),
            Sku = sku,
            Price = 20,
            CategoryId = categoryId,
            StockQuantity = stockQuantity,
            Status = status
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return product;
    }

    private static async Task<Order> SeedOrderAsync(
        ApplicationDbContext context,
        ApplicationUser user,
        Product product,
        string orderNumber,
        OrderStatus status = OrderStatus.Pending,
        DateTime? createdOn = null,
        int quantity = 1)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = user.Id,
            User = user,
            Status = status,
            CreatedOn = createdOn ?? DateTime.UtcNow,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            PaymentStatus = PaymentStatus.Pending,
            SubTotal = product.Price * quantity,
            ShippingCost = 5.99,
            Total = product.Price * quantity + 5.99,
            ShipToName = "Bea Buyer",
            ShipToPhone = "01000000000",
            ShipToLine1 = "1 Main St",
            ShipToCity = "Cairo",
            ShipToState = "Cairo",
            ShipToCountry = "EG",
            Items = new List<OrderItem>
            {
                new OrderItem
                {
                    ProductId = product.Id,
                    Product = product,
                    ProductTitle = product.Title,
                    Sku = product.Sku,
                    UnitPrice = product.Price,
                    Quantity = quantity,
                    LineTotal = product.Price * quantity
                }
            }
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task GetSummaryAsync_computes_total_revenue_excluding_cancelled_orders()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-1", status: OrderStatus.Delivered);
        await SeedOrderAsync(context, user, product, "ORD-2", status: OrderStatus.Cancelled);
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(product.Price + 5.99, result.Value.TotalRevenue, precision: 2);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_all_orders_regardless_of_status()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-1", status: OrderStatus.Pending);
        await SeedOrderAsync(context, user, product, "ORD-2", status: OrderStatus.Cancelled);
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(2, result.Value.TotalOrders);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_only_active_products()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        await SeedProductAsync(context, categoryId, title: "Active", sku: "SKU-A", status: true);
        await SeedProductAsync(context, categoryId, title: "Inactive", sku: "SKU-B", status: false);
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(1, result.Value.ActiveProductCount);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_clients()
    {
        await using var context = CreateContext();
        await SeedUserAsync(context, "a@example.com");
        await SeedUserAsync(context, "b@example.com");
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(2, result.Value.ClientCount);
    }

    [Fact]
    public async Task GetSummaryAsync_counts_low_stock_products()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        await SeedProductAsync(context, categoryId, title: "Low", sku: "SKU-A", stockQuantity: 3);
        await SeedProductAsync(context, categoryId, title: "High", sku: "SKU-B", stockQuantity: 50);
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(1, result.Value.LowStockProductCount);
    }

    [Fact]
    public async Task GetSummaryAsync_returns_the_five_most_recent_orders_newest_first()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        var now = DateTime.UtcNow;
        for (var i = 1; i <= 6; i++)
            await SeedOrderAsync(context, user, product, $"ORD-{i}", createdOn: now.AddDays(-i));
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.Equal(5, result.Value.RecentOrders.Count);
        Assert.Equal("ORD-1", result.Value.RecentOrders[0].OrderNumber);
    }

    [Fact]
    public async Task GetSummaryAsync_returns_zeros_and_empty_lists_for_an_empty_database()
    {
        await using var context = CreateContext();
        var service = new DashboardService(context);

        var result = await service.GetSummaryAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalRevenue);
        Assert.Equal(0, result.Value.TotalOrders);
        Assert.Equal(0, result.Value.ActiveProductCount);
        Assert.Equal(0, result.Value.ClientCount);
        Assert.Equal(0, result.Value.LowStockProductCount);
        Assert.Empty(result.Value.RecentOrders);
    }

    [Fact]
    public async Task GetReportsAsync_counts_orders_by_status_including_zero_for_missing_statuses()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-1", status: OrderStatus.Pending);
        await SeedOrderAsync(context, user, product, "ORD-2", status: OrderStatus.Pending);
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        Assert.Equal(5, result.Value.OrdersByStatus.Count);
        Assert.Equal(2, result.Value.OrdersByStatus.Single(x => x.Status == "Pending").Count);
        Assert.Equal(0, result.Value.OrdersByStatus.Single(x => x.Status == "Delivered").Count);
    }

    [Fact]
    public async Task GetReportsAsync_fills_revenue_by_day_for_the_last_7_days_including_zero_days()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-TODAY", createdOn: DateTime.UtcNow);
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        Assert.Equal(7, result.Value.RevenueByDay.Count);
        var today = result.Value.RevenueByDay[^1];
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), today.Date);
        Assert.Equal(1, today.OrderCount);
        var yesterday = result.Value.RevenueByDay[^2];
        Assert.Equal(0, yesterday.OrderCount);
        Assert.Equal(0, yesterday.Revenue);
    }

    [Fact]
    public async Task GetReportsAsync_excludes_cancelled_orders_from_revenue_by_day()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var product = await SeedProductAsync(context, categoryId);
        await SeedOrderAsync(context, user, product, "ORD-CANCELLED", status: OrderStatus.Cancelled, createdOn: DateTime.UtcNow);
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        var today = result.Value.RevenueByDay[^1];
        Assert.Equal(0, today.OrderCount);
        Assert.Equal(0, today.Revenue);
    }

    [Fact]
    public async Task GetReportsAsync_returns_the_top_products_by_revenue_excluding_cancelled_orders()
    {
        await using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var categoryId = await SeedCategoryAsync(context);
        var popular = await SeedProductAsync(context, categoryId, title: "Popular", sku: "SKU-POP");
        var cancelledOnly = await SeedProductAsync(context, categoryId, title: "CancelledOnly", sku: "SKU-CAN");
        await SeedOrderAsync(context, user, popular, "ORD-1", quantity: 3);
        await SeedOrderAsync(context, user, cancelledOnly, "ORD-2", status: OrderStatus.Cancelled, quantity: 5);
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        Assert.Single(result.Value.TopProducts);
        Assert.Equal("Popular", result.Value.TopProducts[0].ProductTitle);
        Assert.Equal(3, result.Value.TopProducts[0].QuantitySold);
    }

    [Fact]
    public async Task GetReportsAsync_returns_empty_reports_for_an_empty_database()
    {
        await using var context = CreateContext();
        var service = new DashboardService(context);

        var result = await service.GetReportsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.OrdersByStatus.Count);
        Assert.All(result.Value.OrdersByStatus, x => Assert.Equal(0, x.Count));
        Assert.Equal(7, result.Value.RevenueByDay.Count);
        Assert.All(result.Value.RevenueByDay, x => Assert.Equal(0, x.OrderCount));
        Assert.Empty(result.Value.TopProducts);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter DashboardServiceTests
```

Expected: does not compile — `DashboardService`/`IDashboardService` don't exist yet.

- [ ] **Step 5: Implement `DashboardService` and register it in DI**

```csharp
// backend/Ecommerce/Services/DashboardService.cs
using Microsoft.EntityFrameworkCore;
using Ecommerce.Contracts.Dashboard;

namespace Ecommerce.Services;

public class DashboardService(ApplicationDbContext context) : IDashboardService
{
    private const int LowStockThreshold = 5;
    private const int RevenueByDayWindow = 7;
    private const int RecentOrdersCount = 5;
    private const int TopProductsCount = 5;

    private readonly ApplicationDbContext _context = context;

    public async Task<Result<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var totalRevenue = await _context.Orders.AsNoTracking()
            .Where(x => x.Status != OrderStatus.Cancelled)
            .SumAsync(x => x.Total, cancellationToken);

        var totalOrders = await _context.Orders.AsNoTracking().CountAsync(cancellationToken);

        var activeProductCount = await _context.Products.AsNoTracking()
            .Where(x => x.Status)
            .CountAsync(cancellationToken);

        var clientCount = await _context.Users.AsNoTracking().CountAsync(cancellationToken);

        var lowStockProductCount = await _context.Products.AsNoTracking()
            .Where(x => x.StockQuantity <= LowStockThreshold)
            .CountAsync(cancellationToken);

        var recentOrders = await _context.Orders.AsNoTracking()
            .OrderByDescending(x => x.CreatedOn)
            .Take(RecentOrdersCount)
            .Select(x => new RecentOrderResponse(x.OrderNumber, x.ShipToName, x.Status.ToString(), x.Total, x.CreatedOn))
            .ToListAsync(cancellationToken);

        return Result.Success(new DashboardSummaryResponse(
            totalRevenue, totalOrders, activeProductCount, clientCount, lowStockProductCount, recentOrders));
    }

    public async Task<Result<DashboardReportsResponse>> GetReportsAsync(CancellationToken cancellationToken = default)
    {
        var ordersByStatus = await GetOrdersByStatusAsync(cancellationToken);
        var revenueByDay = await GetRevenueByDayAsync(cancellationToken);
        var topProducts = await GetTopProductsAsync(cancellationToken);

        return Result.Success(new DashboardReportsResponse(ordersByStatus, revenueByDay, topProducts));
    }

    private async Task<IReadOnlyList<OrderStatusCountResponse>> GetOrdersByStatusAsync(CancellationToken cancellationToken)
    {
        var counts = await _context.Orders.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return Enum.GetValues<OrderStatus>()
            .Select(status => new OrderStatusCountResponse(
                status.ToString(),
                counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0))
            .ToList();
    }

    // Fetches only the narrow (CreatedOn, Total) shape for the window, then groups/fills
    // missing days in memory — EF Core's DateOnly-from-DateTime grouping doesn't translate
    // cleanly, and the row count here is always tiny (a handful of days of orders).
    private async Task<IReadOnlyList<DailyRevenueResponse>> GetRevenueByDayAsync(CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.Date.AddDays(-(RevenueByDayWindow - 1));

        var recentOrders = await _context.Orders.AsNoTracking()
            .Where(x => x.Status != OrderStatus.Cancelled && x.CreatedOn >= since)
            .Select(x => new { x.CreatedOn, x.Total })
            .ToListAsync(cancellationToken);

        var byDay = recentOrders
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedOn))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(x => x.Total)));

        var sinceDate = DateOnly.FromDateTime(since);
        return Enumerable.Range(0, RevenueByDayWindow)
            .Select(offset => sinceDate.AddDays(offset))
            .Select(date => byDay.TryGetValue(date, out var value)
                ? new DailyRevenueResponse(date, value.Count, value.Revenue)
                : new DailyRevenueResponse(date, 0, 0))
            .ToList();
    }

    // Filters via a subquery on Order.Id rather than accessing OrderItem.Order.Status
    // directly — avoids relying on required-navigation predicate translation, the same
    // defensive posture Phase 4 landed on after its Include-on-a-filtered-navigation defect.
    private async Task<IReadOnlyList<TopProductResponse>> GetTopProductsAsync(CancellationToken cancellationToken)
    {
        var nonCancelledOrderIds = _context.Orders.AsNoTracking()
            .Where(x => x.Status != OrderStatus.Cancelled)
            .Select(x => x.Id);

        return await _context.OrderItems.AsNoTracking()
            .Where(x => nonCancelledOrderIds.Contains(x.OrderId))
            .GroupBy(x => new { x.ProductId, x.ProductTitle })
            .Select(g => new TopProductResponse(g.Key.ProductId, g.Key.ProductTitle, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .OrderByDescending(x => x.Revenue)
            .Take(TopProductsCount)
            .ToListAsync(cancellationToken);
    }
}
```

In `backend/Ecommerce/DependacyInjection.cs`, add one line right after the existing `IOrderAdminService` registration:

```csharp
            services.AddScoped<IOrderAdminService, OrderAdminService>();
            services.AddScoped<IDashboardService, DashboardService>();
```

- [ ] **Step 6: Run tests to verify they pass**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj --filter DashboardServiceTests
```

Expected: 12 passed.

- [ ] **Step 7: Run the full suite and build**

```powershell
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
dotnet build Ecommerce.slnx
```

Expected: 177 passed (165 baseline + 12 new), 0 build errors.

- [ ] **Step 8: Commit**

```bash
git add backend/Ecommerce/Contracts/Dashboard/DashboardResponse.cs backend/Ecommerce/Services/IDashboardService.cs backend/Ecommerce/Services/DashboardService.cs backend/Ecommerce/DependacyInjection.cs backend/Ecommerce.Tests/Services/DashboardServiceTests.cs
git commit -m "Add DashboardService: revenue/order/product/client summary and reports aggregation"
```

---

### Task 2: `AdminDashboardController`

**Files:**
- Create: `backend/Ecommerce/Controllers/AdminDashboardController.cs`
- Test: `backend/Ecommerce.Tests/Authorization/DashboardControllerAuthorizationTests.cs`

**Interfaces:**
- Consumes: `IDashboardService` (Task 1); `PermissionKeys.DashboardView`/`ReportsView` (already exist, already seeded); `AdminAuthDefaults.Scheme`/`PolicyPrefix`; `HasPermissionAttribute`.
- Produces: `GET api/Admin/Dashboard/summary`, `GET api/Admin/Dashboard/reports` — Task 3's `DashboardServices` (frontend) calls both. Both responses `ApiResponse<T>`.

- [ ] **Step 1: Write the controller**

```csharp
// backend/Ecommerce/Controllers/AdminDashboardController.cs
using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Dashboard;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Dashboard")]
[ApiController]
public class AdminDashboardController(IDashboardService dashboardService) : ControllerBase
{
    private readonly IDashboardService _dashboardService = dashboardService;

    [HttpGet("summary")]
    [HasPermission(PermissionKeys.DashboardView)]
    public async Task<IActionResult> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetSummaryAsync(cancellationToken);
        return Ok(new ApiResponse<DashboardSummaryResponse>(StatusCodes.Status200OK, "Dashboard summary loaded.", result.Value));
    }

    [HttpGet("reports")]
    [HasPermission(PermissionKeys.ReportsView)]
    public async Task<IActionResult> GetReportsAsync(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetReportsAsync(cancellationToken);
        return Ok(new ApiResponse<DashboardReportsResponse>(StatusCodes.Status200OK, "Dashboard reports loaded.", result.Value));
    }
}
```

- [ ] **Step 2: Write the authorization test**

```csharp
// backend/Ecommerce.Tests/Authorization/DashboardControllerAuthorizationTests.cs
using System.Reflection;
using Ecommerce.Authorization;
using Ecommerce.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Ecommerce.Tests.Authorization;

public class DashboardControllerAuthorizationTests
{
    [Fact]
    public void AdminDashboardController_requires_the_admin_bearer_scheme()
    {
        var classAuth = typeof(AdminDashboardController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).SingleOrDefault();

        Assert.NotNull(classAuth);
        Assert.Equal(AdminAuthDefaults.Scheme, classAuth!.AuthenticationSchemes);
    }

    [Theory]
    [InlineData("GetSummaryAsync", "DashboardView")]
    [InlineData("GetReportsAsync", "ReportsView")]
    public void AdminDashboardController_actions_require_the_expected_permission(string actionName, string permissionKeyName)
    {
        var action = typeof(AdminDashboardController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!;
        var permission = action.GetCustomAttributes<HasPermissionAttribute>(inherit: true).SingleOrDefault();
        var expectedKey = typeof(PermissionKeys).GetField(permissionKeyName, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!.ToString();

        Assert.NotNull(permission);
        Assert.Equal($"{AdminAuthDefaults.PolicyPrefix}{expectedKey}", permission!.Policy);
    }
}
```

- [ ] **Step 3: Build and run the whole suite**

```powershell
dotnet build Ecommerce.slnx
dotnet test Ecommerce.Tests/Ecommerce.Tests.csproj
```

Expected: 0 build errors, 180 tests passing (177 from Task 1 + 3 new: 1 `Fact` + 1 `Theory` with 2 cases).

- [ ] **Step 4: Manually verify the endpoints**

```powershell
dotnet run --project backend/Ecommerce
```

Log in as `admin.tester@example.com` / `AdminTester@123` via `POST https://localhost:7297/api/Admin/Auth/login`, then:

```bash
curl.exe -k https://localhost:7297/api/Admin/Dashboard/summary
curl.exe -k https://localhost:7297/api/Admin/Dashboard/summary -H "Authorization: Bearer <token>"
curl.exe -k https://localhost:7297/api/Admin/Dashboard/reports -H "Authorization: Bearer <token>"
```

Expected, in order: `401` without a token on the first call; `200` with `totalRevenue`/`totalOrders`/etc. and up to 5 `recentOrders` (matching whatever real orders exist in the dev DB); `200` with `ordersByStatus` (5 rows), `revenueByDay` (7 rows), and `topProducts`.

- [ ] **Step 5: Commit**

```bash
git add backend/Ecommerce/Controllers/AdminDashboardController.cs backend/Ecommerce.Tests/Authorization/DashboardControllerAuthorizationTests.cs
git commit -m "Add AdminDashboardController: summary and reports endpoints"
```

---

### Task 3: Dashboard page (frontend)

**Files:**
- Create: `frontend/src/app/admin/shared/interface/dashboardInterface.ts`
- Create: `frontend/src/app/admin/core/services/dashboard-services.ts`
- Modify: `frontend/src/app/admin/features/pages/dashboard/dashboard.ts`
- Modify: `frontend/src/app/admin/features/pages/dashboard/dashboard.html`
- Modify: `frontend/src/app/admin/features/pages/dashboard/dashboard.scss`
- Modify: `frontend/src/app/app.routes.ts`

**Interfaces:**
- Consumes: `GET api/Admin/Dashboard/summary`, `GET api/Admin/Dashboard/reports` (Task 2); `AdminApiEnvelope<T>` and `AdminAuthServices.hasPermission(key)` (Phase 1); `adminPermissionGuard('dashboard.view')` (Phase 1, not yet applied to this route — this task applies it for the first time).
- Produces: `DashboardSummaryInterface`, `RecentOrderInterface`, `DashboardReportsInterface`, `OrderStatusCountInterface`, `DailyRevenueInterface`, `TopProductInterface`, `DashboardServices`. No component-class rename or route change — `DashboardComponent` keeps its existing name and the existing `path: ''` route, since there is no site-tree collision to alias around (unlike Products/Orders).

- [ ] **Step 1: Write the interfaces**

```typescript
// frontend/src/app/admin/shared/interface/dashboardInterface.ts
export interface RecentOrderInterface {
  orderNumber: string;
  customerName: string;
  status: string;
  total: number;
  createdOn: string;
}

export interface DashboardSummaryInterface {
  totalRevenue: number;
  totalOrders: number;
  activeProductCount: number;
  clientCount: number;
  lowStockProductCount: number;
  recentOrders: RecentOrderInterface[];
}

export interface OrderStatusCountInterface {
  status: string;
  count: number;
}

export interface DailyRevenueInterface {
  date: string;
  orderCount: number;
  revenue: number;
}

export interface TopProductInterface {
  productId: number;
  productTitle: string;
  quantitySold: number;
  revenue: number;
}

export interface DashboardReportsInterface {
  ordersByStatus: OrderStatusCountInterface[];
  revenueByDay: DailyRevenueInterface[];
  topProducts: TopProductInterface[];
}
```

- [ ] **Step 2: Write `DashboardServices`**

```typescript
// frontend/src/app/admin/core/services/dashboard-services.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { DashboardReportsInterface, DashboardSummaryInterface } from '../../shared/interface/dashboardInterface';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class DashboardServices {
  private http = inject(HttpClient);

  getSummary(): Observable<DashboardSummaryInterface> {
    return this.http.get<AdminApiEnvelope<DashboardSummaryInterface>>('/Admin/Dashboard/summary').pipe(map(response => response.data));
  }

  getReports(): Observable<DashboardReportsInterface> {
    return this.http.get<AdminApiEnvelope<DashboardReportsInterface>>('/Admin/Dashboard/reports').pipe(map(response => response.data));
  }
}
```

- [ ] **Step 3: Replace the stub component**

```typescript
// frontend/src/app/admin/features/pages/dashboard/dashboard.ts
import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DashboardServices } from '../../../core/services/dashboard-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { DashboardReportsInterface, DashboardSummaryInterface } from '../../../shared/interface/dashboardInterface';

@Component({
  selector: 'app-admin-dashboard',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardComponent {
  private dashboardService = inject(DashboardServices);
  private auth = inject(AdminAuthServices);

  summary = signal<DashboardSummaryInterface | null>(null);
  summaryLoading = signal(true);
  summaryError = signal('');

  reports = signal<DashboardReportsInterface | null>(null);
  reportsLoading = signal(false);
  reportsError = signal('');

  canViewReports = () => this.auth.hasPermission('reports.view');

  constructor() {
    this.loadSummary();
    if (this.canViewReports()) {
      this.loadReports();
    }
  }

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.dashboardService.getSummary().subscribe({
      next: data => {
        this.summary.set(data);
        this.summaryLoading.set(false);
      },
      error: () => {
        this.summaryLoading.set(false);
        this.summaryError.set('Could not load the dashboard summary. Try again.');
      },
    });
  }

  private loadReports(): void {
    this.reportsLoading.set(true);
    this.dashboardService.getReports().subscribe({
      next: data => {
        this.reports.set(data);
        this.reportsLoading.set(false);
      },
      error: () => {
        this.reportsLoading.set(false);
        this.reportsError.set('Could not load reports. Try again.');
      },
    });
  }
}
```

- [ ] **Step 4: Write the template**

```html
<!-- frontend/src/app/admin/features/pages/dashboard/dashboard.html -->
<h1 class="page-title">Dashboard</h1>
<p class="page-subtitle">An overview of orders, products, and clients.</p>

@if (summaryLoading()) {
  <div class="state-message">Loading dashboard…</div>
} @else if (summaryError()) {
  <div class="alert-error">{{ summaryError() }}</div>
} @else if (summary(); as data) {
  <div class="kpi-grid">
    <div class="kpi-card">
      <span class="kpi-label">Total Revenue</span>
      <span class="kpi-value">{{ data.totalRevenue | currency }}</span>
    </div>
    <div class="kpi-card">
      <span class="kpi-label">Total Orders</span>
      <span class="kpi-value">{{ data.totalOrders }}</span>
    </div>
    <div class="kpi-card">
      <span class="kpi-label">Active Products</span>
      <span class="kpi-value">{{ data.activeProductCount }}</span>
    </div>
    <div class="kpi-card">
      <span class="kpi-label">Clients</span>
      <span class="kpi-value">{{ data.clientCount }}</span>
    </div>
    <div class="kpi-card" [class.kpi-warning]="data.lowStockProductCount > 0">
      <span class="kpi-label">Low Stock</span>
      <span class="kpi-value">{{ data.lowStockProductCount }}</span>
    </div>
  </div>

  <div class="panel">
    <div class="panel-heading">
      <h2 class="section-title">Recent Orders</h2>
      <a routerLink="/admin/orders" class="panel-link">View all orders</a>
    </div>
    @if (data.recentOrders.length) {
      <table class="data-table">
        <thead>
          <tr>
            <th>Order #</th>
            <th>Customer</th>
            <th>Status</th>
            <th>Total</th>
            <th>Date</th>
          </tr>
        </thead>
        <tbody>
          @for (order of data.recentOrders; track order.orderNumber) {
            <tr>
              <td>{{ order.orderNumber }}</td>
              <td>{{ order.customerName }}</td>
              <td><span class="pill" [class.pill-off]="order.status === 'Cancelled'">{{ order.status }}</span></td>
              <td>{{ order.total | currency }}</td>
              <td>{{ order.createdOn | date:'medium' }}</td>
            </tr>
          }
        </tbody>
      </table>
    } @else {
      <p class="state-message">No orders yet.</p>
    }
  </div>
}

@if (canViewReports()) {
  <div class="panel">
    <h2 class="section-title">Reports</h2>

    @if (reportsLoading()) {
      <div class="state-message">Loading reports…</div>
    } @else if (reportsError()) {
      <div class="alert-error">{{ reportsError() }}</div>
    } @else if (reports(); as data) {
      <div class="report-grid">
        <div class="report-block">
          <h3 class="report-title">Orders by Status</h3>
          @if (data.ordersByStatus.length) {
            <table class="data-table">
              <thead><tr><th>Status</th><th>Count</th></tr></thead>
              <tbody>
                @for (row of data.ordersByStatus; track row.status) {
                  <tr><td>{{ row.status }}</td><td>{{ row.count }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="state-message">No orders yet.</p>
          }
        </div>

        <div class="report-block">
          <h3 class="report-title">Revenue — Last 7 Days</h3>
          @if (data.revenueByDay.length) {
            <table class="data-table">
              <thead><tr><th>Date</th><th>Orders</th><th>Revenue</th></tr></thead>
              <tbody>
                @for (row of data.revenueByDay; track row.date) {
                  <tr><td>{{ row.date | date:'mediumDate' }}</td><td>{{ row.orderCount }}</td><td>{{ row.revenue | currency }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="state-message">No revenue data yet.</p>
          }
        </div>

        <div class="report-block">
          <h3 class="report-title">Top Products</h3>
          @if (data.topProducts.length) {
            <table class="data-table">
              <thead><tr><th>Product</th><th>Qty Sold</th><th>Revenue</th></tr></thead>
              <tbody>
                @for (row of data.topProducts; track row.productId) {
                  <tr><td>{{ row.productTitle }}</td><td>{{ row.quantitySold }}</td><td>{{ row.revenue | currency }}</td></tr>
                }
              </tbody>
            </table>
          } @else {
            <p class="state-message">No sales yet.</p>
          }
        </div>
      </div>
    }
  </div>
}
```

- [ ] **Step 5: Write the styles**

```scss
// frontend/src/app/admin/features/pages/dashboard/dashboard.scss
@import '../../../shared/scss/variables';

.page-title {
  font-weight: 800;
  color: $admin-text;
}

.page-subtitle {
  color: $admin-muted;
  margin-bottom: 1.5rem;
}

.kpi-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.kpi-card {
  background: #fff;
  border-radius: $admin-radius;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;

  &.kpi-warning {
    background: rgba(#b3261e, 0.06);
  }
}

.kpi-label {
  color: $admin-muted;
  font-size: 0.8rem;
  text-transform: uppercase;
}

.kpi-value {
  color: $admin-text;
  font-size: 1.6rem;
  font-weight: 800;
}

.panel {
  margin-bottom: 1.5rem;
}

.panel-heading {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.75rem;
}

.panel-link {
  color: $admin-green;
  font-weight: 600;
  font-size: 0.85rem;
  text-decoration: none;

  &:hover { text-decoration: underline; }
}

.section-title {
  font-size: 1.05rem;
  font-weight: 800;
  color: $admin-text;
  margin: 0 0 0.75rem;
}

.report-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 1.5rem;
}

.report-block {
  background: #fff;
  border-radius: $admin-radius;
  padding: 1.25rem;
}

.report-title {
  font-size: 0.9rem;
  font-weight: 700;
  color: $admin-text;
  margin: 0 0 0.75rem;
}

.data-table {
  width: 100%;
  border-collapse: collapse;

  th, td {
    text-align: left;
    padding: 0.6rem 0.5rem;
    border-bottom: 1px solid rgba(0, 0, 0, 0.06);
    font-size: 0.85rem;
  }

  th {
    color: $admin-muted;
    font-size: 0.75rem;
    text-transform: uppercase;
  }
}

.pill {
  background: rgba($admin-green, 0.12);
  color: $admin-green;
  padding: 0.2rem 0.6rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 700;

  &.pill-off {
    background: rgba(#b3261e, 0.1);
    color: #b3261e;
  }
}

.state-message {
  color: $admin-muted;
  padding: 0.75rem 0;
}

.alert-error {
  background: rgba(#b3261e, 0.08);
  color: #b3261e;
  padding: 0.6rem 0.75rem;
  border-radius: 10px;
  font-size: 0.85rem;
}
```

- [ ] **Step 6: Fix the pre-existing missing route guard**

In `frontend/src/app/app.routes.ts`, the dashboard child route currently has no `canActivate`, unlike every other admin route. Change:

```typescript
        { path: '', component: DashboardComponent, title: 'Admin Dashboard' },
```

to:

```typescript
        { path: '', component: DashboardComponent, canActivate: [adminPermissionGuard('dashboard.view')], title: 'Admin Dashboard' },
```

`adminPermissionGuard` is already imported in this file (used by every other admin child route) — no new import needed. No `app.routes.server.ts` change: the base `admin` path already has a `RenderMode.Client` entry (`{ path: 'admin', renderMode: RenderMode.Client }`), which covers this `path: ''` child route — it was never separately listed and doesn't need to be.

- [ ] **Step 7: Type-check**

```powershell
npx tsc --noEmit -p frontend/tsconfig.app.json
```

Expected: 0 errors.

- [ ] **Step 8: Manually verify**

With the backend and frontend running, logged in at `http://127.0.0.1:4200/admin/auth/login` as `admin.tester@example.com` / `AdminTester@123`:

1. `/admin` (the dashboard) shows five KPI cards with real numbers (not the old static placeholder text) and a Recent Orders table with up to 5 rows, each linking correctly via "View all orders" to `/admin/orders`.
2. Below that, a Reports section shows three tables: Orders by Status (5 rows, all `OrderStatus` values, including `0` for any status with no orders), Revenue — Last 7 Days (7 rows, oldest to newest, including `$0.00`/`0` for days with no orders), and Top Products (up to 5 rows).
3. Simulate a `dashboard.view`-only session (same technique used for every prior module: edit the stored `localStorage.shopdemo_admin_auth` permissions in the browser console, removing `reports.view`) and reload `/admin`: the KPI cards and Recent Orders still show, but the Reports section is entirely absent — no failed request, no error message, just gone.
4. Simulate a session with neither `dashboard.view` nor any other permission and try to navigate to `/admin` directly: confirm the route guard now redirects/blocks it (this is the fix from Step 6 — previously this route had no guard at all).

- [ ] **Step 9: Commit**

```bash
git add frontend/src/app/admin/shared/interface/dashboardInterface.ts frontend/src/app/admin/core/services/dashboard-services.ts frontend/src/app/admin/features/pages/dashboard/dashboard.ts frontend/src/app/admin/features/pages/dashboard/dashboard.html frontend/src/app/admin/features/pages/dashboard/dashboard.scss frontend/src/app/app.routes.ts
git commit -m "Add real Dashboard page with KPI summary, recent orders, and reports"
```

---

## Plan-level final check

Once all 3 tasks are done:

- [ ] `dotnet test backend/Ecommerce.Tests/Ecommerce.Tests.csproj` — all passing, including the 12 `DashboardServiceTests` and the 3 new `DashboardControllerAuthorizationTests`, plus everything every prior phase contributed.
- [ ] `dotnet build backend/Ecommerce.slnx` — 0 errors.
- [ ] `npx tsc --noEmit -p frontend/tsconfig.app.json` — 0 errors.
- [ ] `npm run build` from `frontend/` — completes. The dashboard route (`path: ''` under `/admin`) is already covered by the existing `{ path: 'admin', renderMode: RenderMode.Client }` entry — confirm the prerender step doesn't attempt to statically render it. Watch the bundle-budget warning (raised twice already in Phase 2B/3) — if this phase's small addition pushes it into error territory, raise the budget again rather than trimming functionality.
- [ ] **Design-doc coverage sweep.** Confirm in the running app: `AdminDashboardController` at `api/Admin/Dashboard` gated `dashboard.view`/`reports.view` on separate endpoints (not one endpoint with client-side-only report hiding); revenue excludes cancelled orders; low-stock threshold is `<= 5`; orders-by-status always returns 5 rows; revenue-by-day always returns 7 rows; the dashboard route now has its permission guard.
- [ ] **Full manual walkthrough:** admin login → Dashboard (KPI cards, recent orders, reports section) → confirm a `dashboard.view`-only session hides Reports cleanly → confirm the storefront and every other admin page are unaffected (this phase only reads existing tables, touches no other feature's code).
- [ ] **Storefront regression check:** not applicable in the usual sense — this phase adds zero storefront-facing code and modifies no existing service/controller other than adding two new files and one new DI registration line. Confirm `/home`, `/products`, `/checkout`, `/account/orders` still work purely as a sanity check that nothing was accidentally touched.
- [ ] Confirm nothing in this plan set `CreatedById`/`UpdatedById`/`IsDeleted` by hand or threaded an `adminId` through a service:

  ```powershell
  rg -n "IsDeleted\s*=|CreatedById\s*=|UpdatedById\s*=|DeletedById\s*=" backend/Ecommerce/Services backend/Ecommerce/Controllers
  ```

  Expected: no matches.
