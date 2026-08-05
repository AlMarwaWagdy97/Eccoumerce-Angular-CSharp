namespace Ecommerce.Entities;

public class Admin
{
    public long Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public long AdminRoleId { get; set; }
    public AdminRole AdminRole { get; set; } = default!;

    public List<AdminRefreshToken> RefreshTokens { get; set; } = [];
    public List<AdminPasswordResetToken> PasswordResetTokens { get; set; } = [];
}
