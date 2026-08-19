namespace Ecommerce.Errors;

public static class SliderErrors
{
    public static readonly Error SliderNotFound = new("Slider.NotFound", "No slider was found with the given ID");
    public static readonly Error ImageRequired = new("Slider.ImageRequired", "A slider needs an image");
    public static readonly Error InvalidSchedule = new("Slider.InvalidSchedule", "The end date must be after the start date");
}
