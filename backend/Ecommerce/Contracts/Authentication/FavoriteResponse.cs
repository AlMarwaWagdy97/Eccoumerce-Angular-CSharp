namespace Ecommerce.Contracts.Authentication;

public record FavoriteResponse(
    long Id,
    long ProductId,
    string ProductTitle,
    string? ProductImage,
    double Price,
    string Slug);
