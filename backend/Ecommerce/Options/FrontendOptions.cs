using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Options;

public class FrontendOptions
{
    public static string SectionName = "Frontend";

    [Required]
    public string AdminAppUrl { get; init; } = string.Empty;
}
