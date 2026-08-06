namespace Ecommerce.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (requirement.Permissions.Any(permission => context.User.HasClaim("permission", permission)))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
