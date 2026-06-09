using Ecommerce.Contracts.Categories;

namespace Ecommerce.Services
{
    public interface ICategoryService
    {
        Task<IEnumerable<Category>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<Result<CategoryResponse>> GetAsync(long id, CancellationToken cancellationToken = default);
        Task<Result<CategoryResponse>> AddAsync(CategoryRequest request, CancellationToken cancellationToken = default);
        Task<Result> UpdateAsync(long id, CategoryRequest request, CancellationToken cancellationToken = default);
        Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default);
        Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default);
    }
}