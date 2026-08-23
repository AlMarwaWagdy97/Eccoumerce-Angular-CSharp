namespace Ecommerce.Contracts.Products;

public record ProductsPageResponse(
    IReadOnlyList<ProductResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
