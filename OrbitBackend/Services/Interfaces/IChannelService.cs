using OrbitBackend.DTOs.Channel;

namespace OrbitBackend.Services.Interfaces
{
    public interface IChannelService
    {
        /// <summary>
        /// Creates a channel for the user, automatically granting them the Streamer role.
        /// A user can only have one channel.
        /// </summary>
        Task<ChannelResponseDto> CreateChannelAsync(string userId, CreateChannelDto dto);

        /// <summary>
        /// Gets a channel by its ID. Public endpoint.
        /// </summary>
        Task<ChannelResponseDto> GetChannelByIdAsync(int channelId);

        /// <summary>
        /// Gets the channel owned by the authenticated user.
        /// </summary>
        Task<ChannelResponseDto> GetMyChannelAsync(string userId);

        /// <summary>
        /// Hires a moderator for the channel by their username.
        /// Only the channel owner can hire moderators.
        /// </summary>
        Task<ChannelModeratorDto> HireModeratorAsync(string ownerUserId, HireModeratorDto dto);

        /// <summary>
        /// Removes a moderator from the channel by their username.
        /// </summary>
        Task RemoveModeratorAsync(string ownerUserId, string username);

        /// <summary>
        /// Lists all moderators for the channel owned by the given user.
        /// </summary>
        Task<List<ChannelModeratorDto>> GetModeratorsAsync(string ownerUserId);

        // ── Channel Customization ──

        /// <summary>
        /// Updates channel profile info (description, donation, saveStreams).
        /// </summary>
        Task<ChannelResponseDto> UpdateChannelProfileAsync(string userId, UpdateChannelProfileDto dto);

        /// <summary>
        /// Uploads a channel profile photo (avatar) to Cloudinary.
        /// </summary>
        Task<ChannelResponseDto> UploadChannelPhotoAsync(string userId, IFormFile file);

        /// <summary>
        /// Uploads a channel cover/banner image to Cloudinary.
        /// </summary>
        Task<ChannelResponseDto> UploadChannelCoverAsync(string userId, IFormFile file);

        /// <summary>
        /// Replaces all social links for the user's channel.
        /// </summary>
        Task<List<ChannelSocialLinkDto>> UpdateSocialLinksAsync(string userId, UpdateChannelSocialLinksDto dto);

        /// <summary>
        /// Gets social links for any channel by ID.
        /// </summary>
        Task<List<ChannelSocialLinkDto>> GetSocialLinksAsync(int channelId);
    }
}
