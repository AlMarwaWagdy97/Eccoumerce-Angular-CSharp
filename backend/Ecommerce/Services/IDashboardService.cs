using Ecommerce.Contracts.Dashboard;

namespace Ecommerce.Services;

public interface IDashboardService
{
    Task<Result<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<Result<DashboardReportsResponse>> GetReportsAsync(CancellationToken cancellationToken = default);
}
