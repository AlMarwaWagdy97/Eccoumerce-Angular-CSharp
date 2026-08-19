using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Sliders;

namespace Ecommerce.Controllers;

// Storefront-facing, unauthenticated, read-only. Returns only sliders that are
// active AND currently inside their StartsOn/EndsOn window, ordered by Sort —
// the schedule is evaluated server-side so the client needs no date logic.
[Route("api/[controller]")]
[ApiController]
public class SlidersController(ISliderService sliderService) : ControllerBase
{
    private readonly ISliderService _sliderService = sliderService;

    [HttpGet("")]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _sliderService.GetActiveAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<SliderResponse>>(StatusCodes.Status200OK, "Sliders loaded.", result.Value));
    }
}
