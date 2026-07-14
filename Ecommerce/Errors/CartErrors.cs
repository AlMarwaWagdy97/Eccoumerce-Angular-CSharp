namespace Ecommerce.Errors
{
    public class CartErrors
    {
        public static readonly Error CartItemNotFound = new("Cart.ItemNotFound", "No cart item was found with the given product ID");
        public static readonly Error InvalidQuantity = new("Cart.InvalidQuantity", "Quantity must be greater than zero");
        public static readonly Error InsufficientStock = new("Cart.InsufficientStock", "The requested quantity exceeds the available stock");
        public static readonly Error UserNotAuthenticated = new("Cart.UserNotAuthenticated", "The current user could not be identified");
    }
}
