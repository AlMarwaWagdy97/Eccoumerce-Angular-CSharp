namespace Ecommerce.Contracts.Cards;

public record CardResponse(
    long Id,
    string CardholderName,
    string Brand,
    string Last4,
    int ExpiryMonth,
    int ExpiryYear,
    bool IsDefault);
