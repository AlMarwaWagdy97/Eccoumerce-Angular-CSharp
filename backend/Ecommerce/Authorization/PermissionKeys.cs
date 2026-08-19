namespace Ecommerce.Authorization;

public static class PermissionKeys
{
    public const string DashboardView = "dashboard.view";
    public const string CategoriesView = "categories.view";
    public const string CategoriesManage = "categories.manage";
    public const string ClientsView = "clients.view";
    public const string ClientsManage = "clients.manage";
    public const string ProductsView = "products.view";
    public const string ProductsManage = "products.manage";
    public const string OrdersView = "orders.view";
    public const string OrdersManage = "orders.manage";
    public const string SlidersView = "sliders.view";
    public const string SlidersManage = "sliders.manage";
    public const string ReportsView = "reports.view";
    public const string RolesManage = "roles.manage";
    public const string AdminsManage = "admins.manage";

    public static readonly IReadOnlyList<(string Key, string Module, string Description)> Catalog =
    [
        (DashboardView, "Dashboard", "View the dashboard overview"),
        (CategoriesView, "Categories", "View categories"),
        (CategoriesManage, "Categories", "Create, edit, and delete categories"),
        (ClientsView, "Clients", "View customer accounts"),
        (ClientsManage, "Clients", "Edit and toggle customer accounts"),
        (ProductsView, "Products", "View products"),
        (ProductsManage, "Products", "Create, edit, and delete products"),
        (OrdersView, "Orders", "View orders"),
        (OrdersManage, "Orders", "Update order status and details"),
        (SlidersView, "Sliders", "View homepage sliders"),
        (SlidersManage, "Sliders", "Manage homepage sliders"),
        (ReportsView, "Reports", "View sales and product reports"),
        (RolesManage, "Roles", "Create, edit, and delete roles and permissions"),
        (AdminsManage, "Admins", "Create, edit, and delete admin users"),
    ];
}
