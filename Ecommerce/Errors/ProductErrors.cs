namespace Ecommerce.Errors
{
    public class ProductErrors
    {
        public static readonly Error ProductNotFound = new("Product.NotFound", "No product was found with the given ID");
        public static readonly Error DuplicatedProductSku = new("Product.DuplicatedSku", "Another product with the same SKU already exists");
    }
}
