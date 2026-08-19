using Ecommerce.Entities;
using Ecommerce.Presistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Tests.Entities;

public class SliderModelTests
{
    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new NoopHttpContextAccessor());

    [Fact]
    public async Task Slider_round_trips_with_its_scheduling_window()
    {
        await using var context = CreateContext();

        context.Sliders.Add(new Slider
        {
            Title = "Summer sale",
            Image = "/uploads/sliders/summer.jpg",
            Link = "/products",
            Sort = 1,
            Status = true,
            StartsOn = new DateTime(2026, 6, 1),
            EndsOn = new DateTime(2026, 8, 31),
        });
        await context.SaveChangesAsync();

        var loaded = await context.Sliders.FirstAsync(x => x.Title == "Summer sale");

        Assert.Equal("/uploads/sliders/summer.jpg", loaded.Image);
        Assert.Equal(1, loaded.Sort);
        Assert.Equal(new DateTime(2026, 8, 31), loaded.EndsOn);
        Assert.False(loaded.IsDeleted);
    }

    [Fact]
    public async Task Removing_a_slider_soft_deletes_it()
    {
        await using var context = CreateContext();
        context.Sliders.Add(new Slider { Title = "Old", Image = "/uploads/sliders/old.jpg" });
        await context.SaveChangesAsync();

        var slider = await context.Sliders.FirstAsync();
        context.Remove(slider);
        await context.SaveChangesAsync();

        Assert.False(await context.Sliders.AnyAsync(x => x.Id == slider.Id));
        Assert.True(await context.Sliders.IgnoreQueryFilters().AnyAsync(x => x.Id == slider.Id));
    }
}
