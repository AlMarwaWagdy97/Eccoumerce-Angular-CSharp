namespace Ecommerce.Contracts.Categories
{
    public record class CategoryRequest(
        long? ParentId,
        string Title,
        string Slug,
        string? Description,
        string? Image,
        int? Sort,
        string? MetaDescription,
        string? MetaKey
    );
}
