using OrbitBackend.DTOs.UserProfile;

namespace OrbitBackend.Services.Interfaces
{
    public interface IUserProfileService
    {
        Task<UpdateProfilePictureResponseDto> UploadProfilePictureAsync(string userId, IFormFile file);
        Task RemoveProfilePictureAsync(string userId);
        Task<UserPublicProfileDto> GetPublicProfileAsync(string userId);
    }
}
