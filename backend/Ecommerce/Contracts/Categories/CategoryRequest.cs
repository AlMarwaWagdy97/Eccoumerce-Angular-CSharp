namespace Ecommerce.Contracts.Categories
{
    // ImageFile is the multipart upload; Image is the already-stored path.
    // If ImageFile is present it wins and its saved path replaces Image;
    // otherwise Image is kept as-is, which is how "leave the current image
    // alone" is expressed on an update.
    public record class CategoryRequest(
        long? ParentId,
        string Title,
        string Slug,
        string? Description,
        string? Image,
        int? Sort,
        string? MetaDescription,
        string? MetaKey,
        bool? Feature = false,
        bool? Status = true,
        IFormFile? ImageFile = null
    );

    public class CategoryRequestValidator : AbstractValidator<CategoryRequest>
    {
        public CategoryRequestValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.MetaDescription).MaximumLength(500);
            RuleFor(x => x.MetaKey).MaximumLength(255);
        }
    }
}
