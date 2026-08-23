namespace Ecommerce.Contracts.Products
{
    public record class ProductResponse(
        long Id,
        long CategoryId,
        string Title,
        string Slug,
        string Sku,
        double Price,
        double? PriceAfterSale,
        double? Sale,
        string? Image,
        int StockQuantity,
        int? Sort,
        bool Feature,
        bool Status,
        string? MetaDescription,
        string? MetaKey
    );
}
