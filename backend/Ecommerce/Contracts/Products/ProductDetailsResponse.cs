namespace Ecommerce.Contracts.Products
{
    public record ProductReviewResponse(
        long Id,
        string Name,
        int Rating,
        string? Comment,
        DateTime Date
    );

    public record ProductDetailsResponse(
        long Id,
        long CategoryId,
        string? CategoryTitle,
        string Title,
        string Slug,
        string Sku,
        double Price,
        double? PriceAfterSale,
        double? Sale,
        string? Description,
        string? Image,
        IReadOnlyList<string> Images,
        int StockQuantity,
        double? Rating,
        int ReviewsCount,
        IReadOnlyList<ProductReviewResponse> Reviews,
        string? MetaDescription,
        string? MetaKey
    );
}
