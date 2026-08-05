namespace Ecommerce.Contracts.Admins;

public record UpdateAdminRequest(string FirstName, string LastName, string? PhoneNumber, long RoleId, bool IsActive);

public class UpdateAdminRequestValidator : AbstractValidator<UpdateAdminRequest>
{
    public UpdateAdminRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
        RuleFor(x => x.RoleId).GreaterThan(0);
    }
}
