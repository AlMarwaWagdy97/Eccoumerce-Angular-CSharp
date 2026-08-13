namespace Ecommerce.Entities
{
    public class Product : AuditableEntity
    {
        public long Id { get; set; }
        public long CategoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string? Image { get; set; }
        public double Price { get; set; }
        public double? PriceAfterSale { get; set; }
        public double? Sale { get; set; }
        public int StockQuantity { get; set; }
        public int? Sort { get; set; }
        public bool Feature { get; set; } = false;
        public bool Status { get; set; } = true;
        public string? MetaDescription { get; set; }
        public string? MetaKey { get; set; }
        public Category Category { get; set; } = null!;
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
