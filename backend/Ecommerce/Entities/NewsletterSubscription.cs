namespace Ecommerce.Entities;

public sealed class NewsletterSubscription
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
