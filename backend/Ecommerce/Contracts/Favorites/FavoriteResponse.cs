namespace Ecommerce.Contracts.Favorites;

public record FavoriteResponse(
    long Id,
    long ProductId,
    string ProductTitle,
    string? ProductImage,
    double Price,
    string Slug);
