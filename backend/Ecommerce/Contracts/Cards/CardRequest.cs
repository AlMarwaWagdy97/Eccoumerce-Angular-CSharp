namespace Ecommerce.Contracts.Cards;

public record CardRequest(
    string CardholderName,
    string Brand,
    string Last4,
    int ExpiryMonth,
    int ExpiryYear,
    bool IsDefault);
