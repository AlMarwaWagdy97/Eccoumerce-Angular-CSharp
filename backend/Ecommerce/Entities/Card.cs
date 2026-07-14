namespace Ecommerce.Entities;

public sealed class Card
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public string CardholderName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }
    public bool IsDefault { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
