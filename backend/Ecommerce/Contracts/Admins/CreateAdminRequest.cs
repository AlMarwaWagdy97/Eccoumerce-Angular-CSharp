namespace Ecommerce.Contracts.Admins;

public record CreateAdminRequest(string FirstName, string LastName, string Email, string? PhoneNumber, long RoleId);

public class CreateAdminRequestValidator : AbstractValidator<CreateAdminRequest>
{
    public CreateAdminRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.RoleId).GreaterThan(0);
    }
}
