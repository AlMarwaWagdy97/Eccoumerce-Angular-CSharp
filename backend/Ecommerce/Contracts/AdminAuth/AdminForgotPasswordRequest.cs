namespace Ecommerce.Contracts.AdminAuth;

public record AdminForgotPasswordRequest(string Email);

public class AdminForgotPasswordRequestValidator : AbstractValidator<AdminForgotPasswordRequest>
{
    public AdminForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
