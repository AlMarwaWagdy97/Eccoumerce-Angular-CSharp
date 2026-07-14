using Ecommerce.Contracts.Profile;

namespace Ecommerce.Services;

public interface IProfileService
{
    Task<Result<ProfileResponse>> GetAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<ProfileResponse>> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
}
