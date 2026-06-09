namespace Ecommerce.Contracts.Products
{
    public record class ProductRequest(
        long CategoryId,
        string Title,
        string Slug,
        string Sku,
        double Price,
        string? Description,
        string? Image,
        double? PriceAfterSale,
        double? Sale,
        int? Sort,
        string? MetaDescription,
        string? MetaKey
    );
}
