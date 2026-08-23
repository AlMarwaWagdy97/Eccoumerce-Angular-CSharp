using Ecommerce.Authorization;
using Ecommerce.Contracts.Common;
using Ecommerce.Contracts.Products;

namespace Ecommerce.Controllers;

[Authorize(AuthenticationSchemes = AdminAuthDefaults.Scheme)]
[Route("api/Admin/Products")]
[ApiController]
public class AdminProductsController(IProductService productService) : ControllerBase
{
    private readonly IProductService _productService = productService;

    [HttpGet("")]
    [HasPermission(PermissionKeys.ProductsView)]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _productService.GetAdminPageAsync(search, page, pageSize, cancellationToken);
        return Ok(new ApiResponse<ProductsPageResponse>(StatusCodes.Status200OK, "Products loaded.", result.Value));
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionKeys.ProductsView)]
    public async Task<IActionResult> GetByIdAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetAdminDetailAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return NotFound(new ApiResponse<object>(StatusCodes.Status404NotFound, result.Error.Description ?? "Product not found."));

        return Ok(new ApiResponse<AdminProductDetailResponse>(StatusCodes.Status200OK, "Product loaded.", result.Value));
    }

    [HttpPost("")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> CreateAsync([FromForm] ProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _productService.AddAsync(request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not create product."));

        var response = new ApiResponse<ProductResponse>(StatusCodes.Status201Created, "Product created.", result.Value);
        return Created($"/api/Admin/Products/{result.Value.Id}", response);
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> UpdateAsync([FromRoute] long id, [FromForm] ProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(id, request, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not update product."));

        return Ok(new ApiResponse<ProductResponse>(StatusCodes.Status200OK, "Product updated.", result.Value));
    }

    [HttpPut("{id:long}/toggleStatus")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> ToggleStatusAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _productService.ToggleStatusAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not toggle product status."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Product status toggled."));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> DeleteAsync([FromRoute] long id, CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete product."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Product deleted."));
    }

    [HttpPost("{id:long}/images")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> AddImagesAsync([FromRoute] long id, [FromForm] List<IFormFile> imageFiles, CancellationToken cancellationToken)
    {
        var result = await _productService.AddImagesAsync(id, imageFiles, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not add images."));

        return Ok(new ApiResponse<IReadOnlyList<ProductImageResponse>>(StatusCodes.Status200OK, "Images added.", result.Value));
    }

    [HttpDelete("{id:long}/images/{imageId:long}")]
    [HasPermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> DeleteImageAsync([FromRoute] long id, [FromRoute] long imageId, CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteImageAsync(id, imageId, cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new ApiResponse<object>(StatusCodes.Status400BadRequest, result.Error.Description ?? "Could not delete image."));

        return Ok(new ApiResponse<object>(StatusCodes.Status200OK, "Image deleted."));
    }
}
