namespace Ecommerce.Contracts.Cart
{
    public record class CartResponse(
        long Id,
        IReadOnlyList<CartItemResponse> Items,
        int TotalQuantity,
        double TotalPrice
    );
}
