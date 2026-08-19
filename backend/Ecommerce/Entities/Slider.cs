namespace Ecommerce.Entities;

// Homepage carousel slide. Image holds the path returned by IFileStorage
// (e.g. /uploads/sliders/<guid>.jpg). StartsOn/EndsOn are an optional
// scheduling window evaluated server-side by the public endpoint.
public sealed class Slider : AuditableEntity
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string? Link { get; set; }
    public int? Sort { get; set; }
    public bool Status { get; set; } = true;
    public DateTime? StartsOn { get; set; }
    public DateTime? EndsOn { get; set; }
}
