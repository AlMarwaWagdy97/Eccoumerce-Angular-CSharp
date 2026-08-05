using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Email;

public class SmtpOptions
{
    public static string SectionName = "Smtp";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; }

    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    [Required]
    public string FromEmail { get; init; } = string.Empty;

    public string FromName { get; init; } = "ShopDemo Admin";
}
