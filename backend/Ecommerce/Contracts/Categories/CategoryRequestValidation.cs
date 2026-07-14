namespace Ecommerce.Contracts.Categories
{
    public class CategoryRequestValidation : AbstractValidator<Category>
    {
        private readonly ApplicationDbContext _context;

        public CategoryRequestValidation(ApplicationDbContext context)
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(255);

            RuleFor(x => x.Slug)
                .NotEmpty().MaximumLength(255)
                .MustAsync(BeUniqueSlug)
                .WithMessage("{PropertyName} must be unique. This slug already exists.");

            RuleFor(x => x.ParentId)
                .MustAsync(CategoryExists!)
                .When(x => x.ParentId.HasValue)
                .WithMessage("The parent category does not exist.");
        }

        private async Task<bool> BeUniqueSlug(string slug, CancellationToken cancellationToken)
        {
            return !await _context.Categories.AnyAsync(x => x.Slug.ToLower() == slug.ToLower(), cancellationToken);
        }

        private async Task<bool> CategoryExists(long? parentId, CancellationToken cancellationToken)
        {
            return await _context.Categories.AnyAsync(x => x.Id == parentId && x.ParentId == null, cancellationToken);
        }
    }
}
