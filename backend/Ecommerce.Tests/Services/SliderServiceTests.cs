using Ecommerce.Contracts.Sliders;
using Ecommerce.Entities;
using Ecommerce.Errors;
using Ecommerce.Presistence;
using Ecommerce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Services;

public class SliderServiceTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    private static SliderRequest Request(
        string title = "Summer sale",
        string? image = null,
        int? sort = 1,
        bool status = true,
        DateTime? startsOn = null,
        DateTime? endsOn = null,
        IFormFile? imageFile = null) =>
        new(title, image, "/products", sort, status, startsOn, endsOn, imageFile);

    [Fact]
    public async Task CreateAsync_saves_the_uploaded_image_and_returns_its_path()
    {
        await using var context = CreateContext();
        var storage = new StubFileStorage("/uploads/sliders/hero.jpg");
        var service = new SliderService(context, storage);

        var result = await service.CreateAsync(Request(imageFile: TestFiles.Image()));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/sliders/hero.jpg", result.Value.Image);
        Assert.Equal("sliders", storage.LastModule);
    }

    [Fact]
    public async Task CreateAsync_fails_when_no_image_is_supplied()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());

        var result = await service.CreateAsync(Request());

        Assert.False(result.IsSuccess);
        Assert.Equal("Slider.ImageRequired", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_fails_when_the_end_date_precedes_the_start_date()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());

        var result = await service.CreateAsync(Request(
            image: "/uploads/sliders/hero.jpg",
            startsOn: new DateTime(2026, 8, 10),
            endsOn: new DateTime(2026, 8, 1)));

        Assert.False(result.IsSuccess);
        Assert.Equal("Slider.InvalidSchedule", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_propagates_a_file_storage_failure()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage(failWith: FileErrors.TooLarge));

        var result = await service.CreateAsync(Request(imageFile: TestFiles.Image()));

        Assert.False(result.IsSuccess);
        Assert.Equal("File.TooLarge", result.Error.Code);
        Assert.False(await context.Sliders.AnyAsync());
    }

    [Fact]
    public async Task UpdateAsync_keeps_the_existing_image_when_no_new_file_is_uploaded()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        var created = (await service.CreateAsync(Request(image: "/uploads/sliders/original.jpg"))).Value;

        var result = await service.UpdateAsync(created.Id, Request(title: "Renamed"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", result.Value.Title);
        Assert.Equal("/uploads/sliders/original.jpg", result.Value.Image);
    }

    [Fact]
    public async Task GetActiveAsync_excludes_inactive_sliders()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        await service.CreateAsync(Request(title: "Live", image: "/uploads/sliders/a.jpg", status: true));
        await service.CreateAsync(Request(title: "Draft", image: "/uploads/sliders/b.jpg", status: false));

        var result = await service.GetActiveAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Live" }, result.Value.Select(x => x.Title));
    }

    [Fact]
    public async Task GetActiveAsync_excludes_sliders_outside_their_schedule_window()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        var now = DateTime.UtcNow;

        await service.CreateAsync(Request(title: "Always on", image: "/uploads/sliders/a.jpg"));
        await service.CreateAsync(Request(title: "Running", image: "/uploads/sliders/b.jpg", startsOn: now.AddDays(-1), endsOn: now.AddDays(1)));
        await service.CreateAsync(Request(title: "Not started", image: "/uploads/sliders/c.jpg", startsOn: now.AddDays(5)));
        await service.CreateAsync(Request(title: "Expired", image: "/uploads/sliders/d.jpg", endsOn: now.AddDays(-5)));

        var result = await service.GetActiveAsync();

        Assert.True(result.IsSuccess);
        var titles = result.Value.Select(x => x.Title).ToList();
        Assert.Contains("Always on", titles);
        Assert.Contains("Running", titles);
        Assert.DoesNotContain("Not started", titles);
        Assert.DoesNotContain("Expired", titles);
    }

    [Fact]
    public async Task GetActiveAsync_orders_by_sort()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        await service.CreateAsync(Request(title: "Third", image: "/uploads/sliders/c.jpg", sort: 3));
        await service.CreateAsync(Request(title: "First", image: "/uploads/sliders/a.jpg", sort: 1));
        await service.CreateAsync(Request(title: "Second", image: "/uploads/sliders/b.jpg", sort: 2));

        var result = await service.GetActiveAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "First", "Second", "Third" }, result.Value.Select(x => x.Title));
    }

    [Fact]
    public async Task ToggleStatusAsync_flips_the_status()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        var created = (await service.CreateAsync(Request(image: "/uploads/sliders/a.jpg", status: true))).Value;

        var result = await service.ToggleStatusAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.False((await service.GetByIdAsync(created.Id)).Value.Status);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_slider_from_ordinary_queries()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());
        var created = (await service.CreateAsync(Request(image: "/uploads/sliders/a.jpg"))).Value;

        var result = await service.DeleteAsync(created.Id);

        Assert.True(result.IsSuccess);
        Assert.False(await context.Sliders.AnyAsync(x => x.Id == created.Id));
    }

    [Fact]
    public async Task GetByIdAsync_fails_for_an_unknown_slider()
    {
        await using var context = CreateContext();
        var service = new SliderService(context, new StubFileStorage());

        var result = await service.GetByIdAsync(999);

        Assert.False(result.IsSuccess);
        Assert.Equal("Slider.NotFound", result.Error.Code);
    }
}
