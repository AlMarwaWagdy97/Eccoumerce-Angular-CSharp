using Ecommerce.Authentication;
using Ecommerce.Entities;
using Microsoft.Extensions.Options;

namespace Ecommerce.Tests.Authentication;

public class AdminJwtProviderTests
{
    private static AdminJwtProvider CreateProvider() => new(Microsoft.Extensions.Options.Options.Create(new JwtOptions
    {
        Key = "ThisIsAVeryLongAndSecureSecretKeyThatIsAtLeast32CharactersLong",
        Issuer = "EcommerceApp",
        Audience = "EcommerceApp users",
        AdminAudience = "EcommerceApp admin users",
        ExpiryMinutes = 30,
    }));

    private static Admin CreateAdmin() => new()
    {
        Id = 7,
        Email = "test.admin@example.com",
        FirstName = "Test",
        LastName = "Admin",
        AdminRole = new AdminRole { Id = 1, Name = "Manager" },
    };

    [Fact]
    public void GenerateToken_includes_role_and_permission_claims()
    {
        var provider = CreateProvider();
        var admin = CreateAdmin();

        var (token, expiresIn) = provider.GenerateToken(admin, ["products.manage", "orders.view"]);

        Assert.NotEmpty(token);
        Assert.Equal(1800, expiresIn);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("Manager", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.Equal(2, jwt.Claims.Count(c => c.Type == "permission"));
        Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "products.manage");
        Assert.Equal("EcommerceApp admin users", jwt.Audiences.Single());
    }

    [Fact]
    public void ValidateToken_returns_admin_id_for_a_token_it_issued()
    {
        var provider = CreateProvider();
        var (token, _) = provider.GenerateToken(CreateAdmin(), []);

        var adminId = provider.ValidateToken(token);

        Assert.Equal("7", adminId);
    }

    [Fact]
    public void ValidateToken_returns_null_for_garbage_input()
    {
        var provider = CreateProvider();

        Assert.Null(provider.ValidateToken("not-a-real-token"));
    }
}
