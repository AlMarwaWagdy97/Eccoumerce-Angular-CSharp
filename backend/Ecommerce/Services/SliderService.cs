using Ecommerce.Contracts.Sliders;
using Ecommerce.Storage;

namespace Ecommerce.Services;

public class SliderService(ApplicationDbContext context, IFileStorage fileStorage) : ISliderService
{
    private const string StorageModule = "sliders";

    private readonly ApplicationDbContext _context = context;
    private readonly IFileStorage _fileStorage = fileStorage;

    public async Task<Result<IEnumerable<SliderResponse>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sliders = await _context.Sliders.AsNoTracking()
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<SliderResponse>>(sliders.Select(MapSlider).ToList());
    }

    public async Task<Result<IEnumerable<SliderResponse>>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        // Scheduling is evaluated here so the storefront needs no date logic.
        var now = DateTime.UtcNow;

        var sliders = await _context.Sliders.AsNoTracking()
            .Where(x => x.Status
                        && (x.StartsOn == null || x.StartsOn <= now)
                        && (x.EndsOn == null || x.EndsOn >= now))
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<SliderResponse>>(sliders.Select(MapSlider).ToList());
    }

    public async Task<Result<SliderResponse>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var slider = await _context.Sliders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return slider is null
            ? Result.Failure<SliderResponse>(SliderErrors.SliderNotFound)
            : Result.Success(MapSlider(slider));
    }

    public async Task<Result<SliderResponse>> CreateAsync(SliderRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsScheduleValid(request))
            return Result.Failure<SliderResponse>(SliderErrors.InvalidSchedule);

        var imageResult = await ResolveImageAsync(request, currentImage: null, cancellationToken);
        if (!imageResult.IsSuccess)
            return Result.Failure<SliderResponse>(imageResult.Error);

        if (string.IsNullOrWhiteSpace(imageResult.Value))
            return Result.Failure<SliderResponse>(SliderErrors.ImageRequired);

        var slider = new Slider
        {
            Title = request.Title,
            Image = imageResult.Value!,
            Link = request.Link,
            Sort = request.Sort,
            Status = request.Status,
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn,
        };

        await _context.Sliders.AddAsync(slider, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapSlider(slider));
    }

    public async Task<Result<SliderResponse>> UpdateAsync(long id, SliderRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsScheduleValid(request))
            return Result.Failure<SliderResponse>(SliderErrors.InvalidSchedule);

        var slider = await _context.Sliders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (slider is null)
            return Result.Failure<SliderResponse>(SliderErrors.SliderNotFound);

        var imageResult = await ResolveImageAsync(request, slider.Image, cancellationToken);
        if (!imageResult.IsSuccess)
            return Result.Failure<SliderResponse>(imageResult.Error);

        if (string.IsNullOrWhiteSpace(imageResult.Value))
            return Result.Failure<SliderResponse>(SliderErrors.ImageRequired);

        slider.Title = request.Title;
        slider.Image = imageResult.Value!;
        slider.Link = request.Link;
        slider.Sort = request.Sort;
        slider.Status = request.Status;
        slider.StartsOn = request.StartsOn;
        slider.EndsOn = request.EndsOn;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success(MapSlider(slider));
    }

    public async Task<Result> ToggleStatusAsync(long id, CancellationToken cancellationToken = default)
    {
        var slider = await _context.Sliders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (slider is null)
            return Result.Failure(SliderErrors.SliderNotFound);

        slider.Status = !slider.Status;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var slider = await _context.Sliders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (slider is null)
            return Result.Failure(SliderErrors.SliderNotFound);

        // The DbContext hook turns this into a soft delete.
        _context.Remove(slider);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static bool IsScheduleValid(SliderRequest request) =>
        request.StartsOn is null || request.EndsOn is null || request.EndsOn >= request.StartsOn;

    // ImageFile wins if present; otherwise a non-empty Image string wins;
    // otherwise the current stored path is kept unchanged.
    private async Task<Result<string?>> ResolveImageAsync(SliderRequest request, string? currentImage, CancellationToken cancellationToken)
    {
        if (request.ImageFile is not null)
        {
            var saved = await _fileStorage.SaveAsync(request.ImageFile, StorageModule, cancellationToken);
            return saved.IsSuccess
                ? Result.Success<string?>(saved.Value)
                : Result.Failure<string?>(saved.Error);
        }

        return Result.Success(string.IsNullOrWhiteSpace(request.Image) ? currentImage : request.Image);
    }

    private static SliderResponse MapSlider(Slider slider) => new(
        slider.Id, slider.Title, slider.Image, slider.Link, slider.Sort, slider.Status, slider.StartsOn, slider.EndsOn);
}
