namespace Ecommerce.Errors
{
    public static class FavoriteErrors
    {
        public static readonly Error FavoriteNotFound = new("Favorite.NotFound", "No favorite was found with the given product ID");
        public static readonly Error UserNotAuthenticated = new("Favorite.UserNotAuthenticated", "The current user could not be identified");
    }
}
