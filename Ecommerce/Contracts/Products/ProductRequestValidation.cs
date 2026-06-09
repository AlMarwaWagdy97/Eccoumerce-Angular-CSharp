namespace Ecommerce.Contracts.Products
{
    public class ProductRequestValidation : AbstractValidator<Product>
    {
        private readonly ApplicationDbContext _context;

        public ProductRequestValidation(ApplicationDbContext context)
        {
            _context = context;

            RuleFor(x => x.CategoryId).NotEmpty().MustAsync(CategoryExists)
                .WithMessage("The category does not exist.");

            RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Slug).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Price).GreaterThan(0);

            RuleFor(x => x.Sku).NotEmpty().MaximumLength(100).MustAsync(BeUniqueSku)
                .WithMessage("Product with this SKU already exists.");
        }


        private async Task<bool> CategoryExists(long categoryId, CancellationToken cancellationToken)
        {
            return await _context.Categories.AnyAsync(x => x.Id == categoryId, cancellationToken);
        }

        private async Task<bool> BeUniqueSku(string sku, CancellationToken cancellationToken)
        {
            return !await _context.Products.AnyAsync(x => x.Sku.ToLower() == sku.ToLower(), cancellationToken);
        }
    }
}
