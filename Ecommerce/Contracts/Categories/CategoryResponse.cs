namespace Ecommerce.Contracts.Categories
{
    public record class CategoryResponse(
        long Id,
        long? ParentId,
        string Title,
        string Slug,
        string? Description,
        string? Image,
        int? Sort,
        bool Feature,
        bool Status,
        string? MetaDescription,
        string? MetaKey
    );
}