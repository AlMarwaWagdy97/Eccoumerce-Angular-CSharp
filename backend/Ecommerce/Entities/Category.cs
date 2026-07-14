namespace Ecommerce.Entities
{
    public sealed class Category
    {
        public long Id { get; set; }
        public long? ParentId { get; set; } 
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Image { get; set; }
        public int? Sort { get; set; }
        public bool Feature { get; set; } = false; 
        public bool Status { get; set; } = true;    
        public string? MetaDescription { get; set; }
        public string? MetaKey { get; set; }


        public Category? Parent { get; set; }
        public ICollection<Category> SubCategories { get; set; } = new List<Category>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
