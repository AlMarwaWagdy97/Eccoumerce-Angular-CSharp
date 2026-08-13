namespace Ecommerce.Errors;

public class FileErrors
{
    public static readonly Error EmptyFile = new("File.Empty", "No file was uploaded.");
    public static readonly Error UnsupportedType = new("File.UnsupportedType", "Only .jpg, .jpeg, .png and .webp images are allowed.");
    public static readonly Error TooLarge = new("File.TooLarge", "The file exceeds the 2 MB limit.");
}
