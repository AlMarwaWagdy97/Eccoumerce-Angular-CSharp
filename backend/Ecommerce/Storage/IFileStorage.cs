namespace Ecommerce.Storage;

public interface IFileStorage
{
    // Returns the stored file's public relative path, e.g. "/uploads/categories/a1b2....jpg".
    Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken cancellationToken = default);
}
