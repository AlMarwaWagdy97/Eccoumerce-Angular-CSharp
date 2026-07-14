using Microsoft.AspNetCore.Identity;
using Ecommerce.Contracts.Profile;

namespace Ecommerce.Services;

public class ProfileService(UserManager<ApplicationUser> userManager) : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public async Task<Result<ProfileResponse>> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result.Failure<ProfileResponse>(UserErrors.InvalidJwtToken);

        return Result.Success(MapProfile(user));
    }

    public async Task<Result<ProfileResponse>> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Result.Failure<ProfileResponse>(UserErrors.InvalidJwtToken);

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var firstError = result.Errors.First();
            return Result.Failure<ProfileResponse>(new Error(firstError.Code, firstError.Description));
        }

        return Result.Success(MapProfile(user));
    }

    private static ProfileResponse MapProfile(ApplicationUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, user.PhoneNumber);
}
