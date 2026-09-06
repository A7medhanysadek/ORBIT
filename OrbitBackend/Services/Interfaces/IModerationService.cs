using OrbitBackend.DTOs.Moderation;

namespace OrbitBackend.Services.Interfaces
{
    public interface IModerationService
    {
        /// <summary>
        /// Soft-deletes a chat message. Only the channel owner or a channel moderator can do this.
        /// </summary>
        Task<ModerationActionDto> DeleteMessageAsync(int channelId, string moderatorId, long messageId);

        /// <summary>
        /// Times out a user in a channel's chat. Duration is validated against configurable bounds.
        /// </summary>
        Task<ModerationActionDto> TimeoutUserAsync(int channelId, string moderatorId, TimeoutUserDto dto);

        /// <summary>
        /// Permanently bans a user from a channel's chat (until unbanned).
        /// </summary>
        Task<ModerationActionDto> BanUserAsync(int channelId, string moderatorId, BanUserDto dto);

        /// <summary>
        /// Unbans a user from a channel's chat.
        /// </summary>
        Task<ModerationActionDto> UnbanUserAsync(int channelId, string moderatorId, string username);

        /// <summary>
        /// Checks whether a user is currently timed out in a channel.
        /// </summary>
        Task<bool> IsUserTimedOutAsync(int channelId, string userId);

        /// <summary>
        /// Checks whether a user is currently banned in a channel.
        /// </summary>
        Task<bool> IsUserBannedAsync(int channelId, string userId);

        /// <summary>
        /// Validates that the given user has moderation privileges for the channel
        /// (is the channel owner or a hired moderator).
        /// </summary>
        Task<bool> HasModerationPrivilegesAsync(int channelId, string userId);
    }
}
