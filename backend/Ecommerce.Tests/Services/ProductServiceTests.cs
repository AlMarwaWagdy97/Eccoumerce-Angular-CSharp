using Ecommerce.Contracts.Products;
using Ecommerce.Entities;
using Ecommerce.Errors;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class ProductServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static async Task<long> SeedCategoryAsync(ApplicationDbContext context, string title = "Shoes")
    {
        var category = new Category { Title = title, Slug = title.ToLower() };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static ProductRequest Request(
        long categoryId,
        string title = "Runner",
        string slug = "runner",
        string sku = "SKU-1",
        double price = 50,
        string? image = null,
        int? stockQuantity = null,
        bool? status = null,
        bool? feature = null,
        IFormFile? imageFile = null) =>
        new(categoryId, title, slug, sku, price, null, image, null, null, stockQuantity, 1, status, feature, null, null, imageFile);

    [Fact]
    public async Task AddAsync_saves_the_uploaded_image_and_uses_its_path()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var storage = new StubFileStorage("/uploads/products/hero.jpg");
        var service = new ProductService(context, storage);

        var result = await service.AddAsync(Request(categoryId, imageFile: TestFiles.Image()));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/products/hero.jpg", result.Value.Image);
        Assert.Equal("products", storage.LastModule);
    }

    [Fact]
    public async Task AddAsync_keeps_the_supplied_image_string_when_no_file_is_uploaded()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());

        var result = await service.AddAsync(Request(categoryId, image: "/uploads/products/seeded.png"));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/products/seeded.png", result.Value.Image);
    }

    [Fact]
    public async Task AddAsync_honours_stock_status_and_feature_instead_of_hardcoding_them()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());

        var result = await service.AddAsync(Request(categoryId, stockQuantity: 25, status: false, feature: true));

        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Value.StockQuantity);
        Assert.False(result.Value.Status);
        Assert.True(result.Value.Feature);
    }

    [Fact]
    public async Task AddAsync_defaults_stock_to_zero_and_status_to_active_when_omitted()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());

        var result = await service.AddAsync(Request(categoryId));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.StockQuantity);
        Assert.True(result.Value.Status);
        Assert.False(result.Value.Feature);
    }

    [Fact]
    public async Task AddAsync_propagates_a_file_storage_failure()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage(failWith: FileErrors.UnsupportedType));

        var result = await service.AddAsync(Request(categoryId, imageFile: TestFiles.Image("virus.exe")));

        Assert.False(result.IsSuccess);
        Assert.Equal("File.UnsupportedType", result.Error.Code);
        Assert.False(await context.Products.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_keeps_the_existing_image_when_neither_a_file_nor_an_image_path_is_supplied()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request(categoryId, image: "/uploads/products/original.jpg"))).Value;

        var result = await service.UpdateAsync(created.Id, Request(categoryId, title: "Renamed", slug: "renamed", sku: created.Sku));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value.Title);
        Assert.Equal("/uploads/products/original.jpg", result.Value.Image);
    }

    [Fact]
    public async Task UpdateAsync_keeps_the_existing_stock_and_status_when_omitted()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        var created = (await service.AddAsync(Request(categoryId, stockQuantity: 40, status: false))).Value;

        var result = await service.UpdateAsync(created.Id, Request(categoryId, title: "Renamed", sku: created.Sku));

        Assert.True(result.IsSuccess);
        Assert.Equal(40, result.Value.StockQuantity);
        Assert.False(result.Value.Status);
    }

    [Fact]
    public async Task GetAdminPageAsync_filters_by_title_or_sku()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        await service.AddAsync(Request(categoryId, title: "Blue Runner", slug: "blue-runner", sku: "RUN-BLU"));
        await service.AddAsync(Request(categoryId, title: "Red Sandals", slug: "red-sandals", sku: "SAN-RED"));

        var result = await service.GetAdminPageAsync("run", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal("Blue Runner", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task GetAdminPageAsync_pages_the_result_set()
    {
        await using var context = CreateContext();
        var categoryId = await SeedCategoryAsync(context);
        var service = new ProductService(context, new StubFileStorage());
        await service.AddAsync(Request(categoryId, title: "A", slug: "a", sku: "SKU-A"));
        await service.AddAsync(Request(categoryId, title: "B", slug: "b", sku: "SKU-B"));
        await service.AddAsync(Request(categoryId, title: "C", slug: "c", sku: "SKU-C"));

        var result = await service.GetAdminPageAsync(null, 2, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Equal(2, result.Value.TotalPages);
        Assert.Single(result.Value.Items);
    }
}
