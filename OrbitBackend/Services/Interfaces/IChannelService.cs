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
    }
}
