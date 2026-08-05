namespace Ecommerce.Authorization;

public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(AdminAuthDefaults.PolicyPrefix, StringComparison.Ordinal))
            return _fallback.GetPolicyAsync(policyName);

        var permission = policyName[AdminAuthDefaults.PolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder(AdminAuthDefaults.Scheme)
            .AddRequirements(new PermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
