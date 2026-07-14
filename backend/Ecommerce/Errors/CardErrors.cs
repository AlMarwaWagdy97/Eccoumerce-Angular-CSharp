namespace Ecommerce.Errors
{
    public static class CardErrors
    {
        public static readonly Error CardNotFound = new("Card.NotFound", "No card was found with the given ID");
        public static readonly Error UserNotAuthenticated = new("Card.UserNotAuthenticated", "The current user could not be identified");
    }
}
