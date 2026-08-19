namespace Ecommerce.Contracts.Sliders;

public record SliderResponse(
    long Id,
    string Title,
    string Image,
    string? Link,
    int? Sort,
    bool Status,
    DateTime? StartsOn,
    DateTime? EndsOn);
