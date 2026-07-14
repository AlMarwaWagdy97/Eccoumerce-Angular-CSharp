namespace Ecommerce.Errors
{
    public static class AddressErrors
    {
        public static readonly Error AddressNotFound = new("Address.NotFound", "No address was found with the given ID");
        public static readonly Error UserNotAuthenticated = new("Address.UserNotAuthenticated", "The current user could not be identified");
    }
}
