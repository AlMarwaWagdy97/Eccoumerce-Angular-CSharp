using Ecommerce.Contracts.Products;

namespace Ecommerce.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<Result<ProductResponse>> GetAsync(long id, CancellationToken cancellationToken = default);
        Task<Result<ProductResponse>> AddAsync(ProductRequest request, CancellationToken cancellationToken = default);
        Task<Result> UpdateAsync(long id, ProductRequest request, CancellationToken cancellationToken = default);
        Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
        Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default);
    }
}
