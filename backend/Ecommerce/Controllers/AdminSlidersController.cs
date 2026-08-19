using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Sliders;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Sliders")]
[ApiController]
public class AdminSlidersController(ISliderService sliderService) : ControllerBase
{
    private readonly ISliderService _sliderService = sliderService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.SlidersView)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var result = await _sliderService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<IEnumerable<SliderResponse>>(StatusCodes.Status200OK, "Sliders loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionKeys.SlidersView)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _sliderService.GetByIdAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Slider not found."));

        return Ok(new ApiResponse<SliderResponse>(StatusCodes.Status200OK, "Slider loaded.", result.Value));
    }

    [HttpPost("")]
    [HasPermission(PermissionKeys.SlidersManage)]
    public async Task<IActionResult> CreateAsync([FromForm] SliderRequest request, CancellationToken cancellationToken)
    {
        var result = await _sliderService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create slider."));

        var response = new ApiResponse<SliderResponse>(StatusCodes.Status201Created, "Slider created.", result.Value);
        return Created($"/api/Admin/Sliders/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionKeys.SlidersManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromForm] SliderRequest request, CancellationToken cancellationToken)
    {
        var result = await _sliderService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update slider."));

        return Ok(new ApiResponse<SliderResponse>(StatusCodes.Status200OK, "Slider updated.", result.Value));
    }

    [HttpPut("{id:long}/toggleStatus")]
    [HasPermission(PermissionKeys.SlidersManage)]
    public async Task<IActionResult> ToggleStatusAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _sliderService.ToggleStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not toggle slider status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Slider status toggled."));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionKeys.SlidersManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _sliderService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete slider."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Slider deleted."));
    }
}
