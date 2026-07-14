namespace Ecommerce.Errors
{
    public static class OrderErrors
    {
        public static readonly Error OrderNotFound = new("Order.NotFound", "No order was found with the given order number");
        public static readonly Error EmptyOrderItems = new("Order.EmptyItems", "An order must contain at least one item");
        public static readonly Error AddressNotFound = new("Order.AddressNotFound", "The selected shipping address was not found");
        public static readonly Error ProductNotFound = new("Order.ProductNotFound", "One or more products in the order were not found");
        public static readonly Error InsufficientStock = new("Order.InsufficientStock", "One or more products do not have enough stock");
        public static readonly Error UserNotAuthenticated = new("Order.UserNotAuthenticated", "The current user could not be identified");
    }
}
