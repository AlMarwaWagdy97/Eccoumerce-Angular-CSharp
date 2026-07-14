namespace Ecommerce.Contracts.Cart
{
    public record class AddToCartRequest(
        long ProductId,
        int Quantity
    );
}
