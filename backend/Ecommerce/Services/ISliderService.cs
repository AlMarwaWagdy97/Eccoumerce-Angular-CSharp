using Ecommerce.Contracts.Sliders;

namespace Ecommerce.Services;

public interface ISliderService
{
    // Admin view: every non-deleted slider, whatever its status or schedule.
    Task<Result<IEnumerable<SliderResponse>>> GetAllAsync(CancellationToken cancellationToken = default);

    // Storefront view: active AND currently within its schedule window, ordered by Sort.
    Task<Result<IEnumerable<SliderResponse>>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<Result<SliderResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<SliderResponse>> CreateAsync(SliderRequest request, CancellationToken cancellationToken = default);
    Task<Result<SliderResponse>> UpdateAsync(long id, SliderRequest request, CancellationToken cancellationToken = default);
    Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
}
