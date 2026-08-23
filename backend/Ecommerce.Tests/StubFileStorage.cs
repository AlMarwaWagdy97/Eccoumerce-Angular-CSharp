using Ecommerce.Abstractions;
using Ecommerce.Storage;
using Microsoft.AspNetCore.Http;

namespace Ecommerce.Tests;

// Test double for Plan 2A's IFileStorage: records what it was called with and
// returns a deterministic path, so services can be tested without touching disk.
public class StubFileStorage(string savedPath = "/uploads/test/stub.jpg", Error? failWith = null) : IFileStorage
{
    private string _savedPath = savedPath;
    private readonly Error? _failWith = failWith;

    public string? LastModule { get; private set; }
    public int SaveCallCount { get; private set; }

    // Lets a single stub return a different path on the next call — needed when a
    // test uploads more than one file and must tell the results apart by URL.
    public void SetNextPath(string path) => _savedPath = path;

    public Task<Result<string>> SaveAsync(IFormFile file, string module, CancellationToken cancellationToken = default)
    {
        LastModule = module;
        SaveCallCount++;

        return Task.FromResult(_failWith is null
            ? Result.Success(_savedPath)
            : Result.Failure<string>(_failWith));
    }
}

public static class TestFiles
{
    public static IFormFile Image(string fileName = "photo.jpg")
    {
        var bytes = new byte[] { 1, 2, 3, 4 };

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ImageFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg",
        };
    }
}
