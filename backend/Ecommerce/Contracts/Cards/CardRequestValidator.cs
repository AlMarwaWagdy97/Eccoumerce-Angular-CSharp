namespace Ecommerce.Contracts.Cards;

public class CardRequestValidator : AbstractValidator<CardRequest>
{
    public CardRequestValidator()
    {
        RuleFor(x => x.CardholderName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(30);

        RuleFor(x => x.Last4)
            .NotEmpty()
            .Matches("^[0-9]{4}$")
            .WithMessage("Last4 must be exactly 4 digits.");

        RuleFor(x => x.ExpiryMonth).InclusiveBetween(1, 12);
        RuleFor(x => x.ExpiryYear).GreaterThanOrEqualTo(DateTime.UtcNow.Year);

        RuleFor(x => x)
            .Must(NotBeExpired)
            .WithMessage("Card expiry date must be in the future.")
            .WithName("Expiry");
    }

    private static bool NotBeExpired(CardRequest request)
    {
        if (request.ExpiryMonth is < 1 or > 12)
            return true; // covered by the ExpiryMonth rule above

        var expiry = new DateTime(request.ExpiryYear, request.ExpiryMonth, 1).AddMonths(1);
        return expiry > DateTime.UtcNow;
    }
}
