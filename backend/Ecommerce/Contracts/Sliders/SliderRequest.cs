namespace Ecommerce.Contracts.Sliders;

// ImageFile is the multipart upload; Image is the already-stored path.
// If ImageFile is present it wins and its saved path replaces Image;
// otherwise Image is kept as-is, which is how "leave the current image
// alone" is expressed on an update.
public record SliderRequest(
    string Title,
    string? Image,
    string? Link,
    int? Sort,
    bool Status,
    DateTime? StartsOn,
    DateTime? EndsOn,
    IFormFile? ImageFile = null);

public class SliderRequestValidator : AbstractValidator<SliderRequest>
{
    public SliderRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Link).MaximumLength(500);
    }
}
