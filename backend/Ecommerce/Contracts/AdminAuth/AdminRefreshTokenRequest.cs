namespace Ecommerce.Contracts.AdminAuth;

public record AdminRefreshTokenRequest(string Token, string RefreshToken);

public class AdminRefreshTokenRequestValidator : AbstractValidator<AdminRefreshTokenRequest>
{
    public AdminRefreshTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
