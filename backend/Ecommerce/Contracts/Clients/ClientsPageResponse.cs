namespace Ecommerce.Contracts.Clients;

public record ClientsPageResponse(
    IReadOnlyList<ClientResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
