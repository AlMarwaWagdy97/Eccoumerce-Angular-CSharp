namespace Ecommerce.Contracts.Roles;

public record RoleRequest(string Name, string? Description, List<string> PermissionKeys);

public class RoleRequestValidator : AbstractValidator<RoleRequest>
{
    public RoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.PermissionKeys).NotNull();
    }
}
