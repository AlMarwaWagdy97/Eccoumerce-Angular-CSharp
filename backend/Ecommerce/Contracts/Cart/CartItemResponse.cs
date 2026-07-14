namespace Ecommerce.Contracts.Cart
{
    public record class CartItemResponse(
        long Id,
        long ProductId,
        string Title,
        string? Image,
        double UnitPrice,
        int Quantity,
        double LineTotal
    );
}
