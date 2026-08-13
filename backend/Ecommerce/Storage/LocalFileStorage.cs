namespace Ecommerce.Storage;

// Writes under wwwroot/uploads/<module>/ and returns the path the browser will request.
// Replacing an image writes a new file and leaves the old one on disk: a soft-deleted
// record may be restored later, so deleting its image would be unrecoverable.
public class LocalFileStorage(IWebHostEnvironment environment) : IFileStorage
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxBytes = 2 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment = environment;

    public async Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
            return Result.Failure<string>(FileErrors.EmptyFile);

        if (file.Length > MaxBytes)
            return Result.Failure<string>(FileErrors.TooLarge);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return Result.Failure<string>(FileErrors.UnsupportedType);

        // WebRootPath is null when wwwroot does not exist yet; fall back to where it will be.
        var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, "uploads", module);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";

        await using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
            await file.CopyToAsync(stream, cancellationToken);

        return Result.Success($"/uploads/{module}/{fileName}");
    }
}
