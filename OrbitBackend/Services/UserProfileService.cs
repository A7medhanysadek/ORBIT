using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.UserProfile;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly ILogger<UserProfileService> _logger;

        public UserProfileService(
            UserManager<AppUser> userManager,
            AppDbContext context,
            ICloudinaryService cloudinaryService,
            ILogger<UserProfileService> logger)
        {
            _userManager = userManager;
            _context = context;
            _cloudinaryService = cloudinaryService;
            _logger = logger;
        }

        public async Task<UpdateProfilePictureResponseDto> UploadProfilePictureAsync(string userId, IFormFile file)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            // Delete old picture from Cloudinary if exists
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                var oldPublicId = _cloudinaryService.GetPublicIdFromUrl(user.ProfilePictureUrl);
                if (!string.IsNullOrEmpty(oldPublicId))
                    await _cloudinaryService.DeleteImageAsync(oldPublicId);
            }

            var url = await _cloudinaryService.UploadImageAsync(file, "orbit/profile-pictures");
            user.ProfilePictureUrl = url;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Profile picture updated for user {UserId}.", userId);

            return new UpdateProfilePictureResponseDto
            {
                ProfilePictureUrl = url,
                Message = "Profile picture updated successfully."
            };
        }

        public async Task RemoveProfilePictureAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (string.IsNullOrEmpty(user.ProfilePictureUrl))
                throw new InvalidOperationException("No profile picture to remove.");

            var publicId = _cloudinaryService.GetPublicIdFromUrl(user.ProfilePictureUrl);
            if (!string.IsNullOrEmpty(publicId))
                await _cloudinaryService.DeleteImageAsync(publicId);

            user.ProfilePictureUrl = null;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Profile picture removed for user {UserId}.", userId);
        }

        public async Task<UserPublicProfileDto> GetPublicProfileAsync(string userId)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.Channel)
                .FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new InvalidOperationException("User not found.");

            return new UserPublicProfileDto
            {
                UserId = user.Id,
                Username = user.UserName!,
                FullName = user.FullName,
                ProfilePictureUrl = user.ProfilePictureUrl,
                ChannelId = user.Channel?.Id,
                ChannelName = user.Channel?.ChannelName
            };
        }
    }
}
