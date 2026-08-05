namespace Ecommerce.Entities;

[Owned]
public class AdminPasswordResetToken
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresOn { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? UsedOn { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresOn;
    public bool IsUsable => UsedOn is null && !IsExpired;
}
