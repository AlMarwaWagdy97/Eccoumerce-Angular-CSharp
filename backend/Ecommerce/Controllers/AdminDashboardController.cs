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
