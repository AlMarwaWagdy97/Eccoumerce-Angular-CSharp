using System.Text;
using Ecommerce.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Ecommerce.Tests.Storage;

public class LocalFileStorageTests
{
    private static (LocalFileStorage Storage, string Root) CreateStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecom-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.WebRootPath).Returns(root);
        environment.SetupGet(x => x.ContentRootPath).Returns(root);

        return (new LocalFileStorage(environment.Object), root);
    }

    private static IFormFile FileNamed(string fileName, int byteCount = 8)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', byteCount)));
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }

    [Fact]
    public async Task SaveAsync_writes_the_file_and_returns_its_public_path()
    {
        var (storage, root) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed("banner.png"), "sliders");

        Assert.True(result.IsSuccess);
        Assert.StartsWith("/uploads/sliders/", result.Value);
        Assert.EndsWith(".png", result.Value);

        var written = Path.Combine(root, result.Value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(written));
    }

    [Fact]
    public async Task SaveAsync_rejects_an_empty_file()
    {
        var (storage, _) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed("banner.png", byteCount: 0), "sliders");

        Assert.False(result.IsSuccess);
        Assert.Equal("File.Empty", result.Error.Code);
    }

    [Fact]
    public async Task SaveAsync_rejects_an_unsupported_extension()
    {
        var (storage, _) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed("payload.svg"), "sliders");

        Assert.False(result.IsSuccess);
        Assert.Equal("File.UnsupportedType", result.Error.Code);
    }

    [Fact]
    public async Task SaveAsync_rejects_a_file_over_two_megabytes()
    {
        var (storage, _) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed("huge.png", byteCount: 2 * 1024 * 1024 + 1), "sliders");

        Assert.False(result.IsSuccess);
        Assert.Equal("File.TooLarge", result.Error.Code);
    }

    [Theory]
    [InlineData("photo.JPG")]
    [InlineData("photo.jpeg")]
    [InlineData("photo.WebP")]
    public async Task SaveAsync_accepts_the_allowed_extensions_case_insensitively(string fileName)
    {
        var (storage, _) = CreateStorage();

        var result = await storage.SaveAsync(FileNamed(fileName), "categories");

        Assert.True(result.IsSuccess);
    }
}
