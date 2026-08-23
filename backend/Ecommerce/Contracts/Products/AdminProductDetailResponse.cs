namespace Ecommerce.Contracts.Products;

public record AdminProductDetailResponse(
    long Id,
    long CategoryId,
    string Title,
    string Slug,
    string Sku,
    double Price,
    double? PriceAfterSale,
    double? Sale,
    string? Description,
    string? Image,
    IReadOnlyList<ProductImageResponse> Images,
    int StockQuantity,
    int? Sort,
    bool Feature,
    bool Status,
    string? MetaDescription,
    string? MetaKey);
