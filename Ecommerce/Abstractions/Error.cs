namespace Ecommerce.Abstractions;

public record Error(string Code, string Description)
{
    public static readonly Error None = null!;  

}