using Ecommerce.Contracts.Categories;
using Ecommerce.Entities;
using Ecommerce.Errors;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class CategoryServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static CategoryRequest Request(
        string title = "Shoes",
        string slug = "shoes",
        string? image = null,
        long? parentId = null,
        IFormFile? imageFile = null) =>
        new(parentId, title, slug, null, image, 1, null, null, false, true, imageFile);

    [Fact]
    public async Task AddAsync_saves_the_uploaded_image_and_uses_its_path()
    {
        await using var context = CreateContext();
        var storage = new StubFileStorage("/uploads/categories/abc.jpg");
        var service = new CategoryService(context, storage);

        var result = await service.AddAsync(Request(imageFile: TestFiles.Image()));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/categories/abc.jpg", result.Value.Image);
        Assert.Equal("categories", storage.LastModule);
    }

    [Fact]
    public async Task AddAsync_keeps_the_supplied_image_string_when_no_file_is_uploaded()
    {
        await using var context = CreateContext();
        var storage = new StubFileStorage();
        var service = new CategoryService(context, storage);

        var result = await service.AddAsync(Request(image: "/uploads/categories/seeded.png"));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/categories/seeded.png", result.Value.Image);
        Assert.Equal(0, storage.SaveCallCount);
    }

    [Fact]
    public async Task AddAsync_fails_for_a_duplicate_slug()
    {
        await using var context = CreateContext();
        var service = new CategoryService(context, new StubFileStorage());
        await service.AddAsync(Request(slug: "shoes"));

        var result = await service.AddAsync(Request(title: "Other shoes", slug: "shoes"));

        Assert.False(result.IsSuccess);
        Assert.Equal("Category.DuplicatedSlug", result.Error.Code);
    }

    [Fact]
    public async Task AddAsync_propagates_a_file_storage_failure()
    {
        await using var context = CreateContext();
        var storage = new StubFileStorage(failWith: FileErrors.UnsupportedType);
        var service = new CategoryService(context, storage);

        var result = await service.AddAsync(Request(imageFile: TestFiles.Image("virus.exe")));

        Assert.False(result.IsSuccess);
        Assert.Equal("File.UnsupportedType", result.Error.Code);
        Assert.False(await context.Categories.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_keeps_the_existing_image_when_neither_a_file_nor_an_image_path_is_supplied()
    {
        await using var context = CreateContext();
        var service = new CategoryService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request(image: "/uploads/categories/original.jpg"))).Value;

        var result = await service.UpdateAsync(created.Id, Request(title: "Renamed", slug: "renamed"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value.Title);
        Assert.Equal("/uploads/categories/original.jpg", result.Value.Image);
    }

    [Fact]
    public async Task UpdateAsync_fails_when_a_category_is_made_its_own_parent()
    {
        await using var context = CreateContext();
        var service = new CategoryService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request())).Value;

        var result = await service.UpdateAsync(created.Id, Request(parentId: created.Id));

        Assert.False(result.IsSuccess);
        Assert.Equal("Category.InvalidParent", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_category_from_ordinary_queries()
    {
        await using var context = CreateContext();
        var service = new CategoryService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request())).Value;

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Categories.AnyAsync(x => x.Id == created.Id));
    }
}
