namespace Ecommerce.Errors
{
    public class CategoryErrors
    {
        public static readonly Error CategoryNotFound = new("404", "No category was found with the given ID");
        public static readonly Error DuplicatedCategorySlug = new("Category.DuplicatedSlug", "Another category with the same slug already exists");
    }
}
