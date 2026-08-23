namespace Ecommerce.Contracts.Products
{
    // ImageFile is the multipart upload; Image is the already-stored path.
    // If ImageFile is present it wins and its saved path replaces Image;
    // otherwise Image is kept as-is, which is how "leave the current image
    // alone" is expressed on an update. StockQuantity/Status/Feature are
    // nullable so an update that omits them keeps the current value.
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
        int? StockQuantity,
        int? Sort,
        bool? Status,
        bool? Feature,
        string? MetaDescription,
        string? MetaKey,
        IFormFile? ImageFile = null
    );

    public class ProductRequestValidator : AbstractValidator<ProductRequest>
    {
        public ProductRequestValidator()
        {
            RuleFor(x => x.CategoryId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Sku).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Price).GreaterThan(0);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.MetaDescription).MaximumLength(500);
            RuleFor(x => x.MetaKey).MaximumLength(255);
        }
    }
}
