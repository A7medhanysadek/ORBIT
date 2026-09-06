using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Moderation;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class ModerationService : IModerationService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ILogger<ModerationService> _logger;

        public ModerationService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IConfiguration config,
            ILogger<ModerationService> logger)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _logger = logger;
        }

        public async Task<ModerationActionDto> DeleteMessageAsync(int channelId, string moderatorId, long messageId)
        {
            await EnsureModerationPrivileges(channelId, moderatorId);

            var message = await _context.ChatMessages
                .Include(m => m.LiveStream)
                .FirstOrDefaultAsync(m => m.Id == messageId && m.LiveStream.ChannelId == channelId)
                ?? throw new InvalidOperationException("Message not found in this channel.");

            if (message.IsDeleted)
                throw new InvalidOperationException("Message is already deleted.");

            message.IsDeleted = true;
            message.DeletedByUserId = moderatorId;
            await _context.SaveChangesAsync();

            var moderator = await _userManager.FindByIdAsync(moderatorId);

            _logger.LogInformation(
                "Message {MessageId} deleted by moderator {ModeratorId} in channel {ChannelId}.",
                messageId, moderatorId, channelId);

            return new ModerationActionDto
            {
                Action = "DeleteMessage",
                TargetUsername = message.SenderName,
                ModeratorName = moderator?.FullName ?? "Unknown",
                Timestamp = DateTime.UtcNow,
                Message = "Message deleted successfully."
            };
        }

        public async Task<ModerationActionDto> TimeoutUserAsync(int channelId, string moderatorId, TimeoutUserDto dto)
        {
            await EnsureModerationPrivileges(channelId, moderatorId);

            // Validate duration against configurable bounds
            var minTimeout = _config.GetValue("Moderation:MinTimeoutSeconds", 10);
            var maxTimeout = _config.GetValue("Moderation:MaxTimeoutSeconds", 86400); // 24 hours default

            if (dto.DurationSeconds < minTimeout || dto.DurationSeconds > maxTimeout)
                throw new InvalidOperationException(
                    $"Timeout duration must be between {minTimeout} and {maxTimeout} seconds.");

            var targetUser = await _userManager.FindByNameAsync(dto.Username)
                ?? throw new InvalidOperationException($"User '{dto.Username}' not found.");

            // Prevent timing out the channel owner
            var channel = await _context.Channels.FindAsync(channelId)
                ?? throw new InvalidOperationException("Channel not found.");

            if (targetUser.Id == channel.OwnerId)
                throw new InvalidOperationException("You cannot time out the channel owner.");

            // Prevent timing out other moderators (only the owner can)
            var targetIsMod = await _context.ChannelModerators
                .AnyAsync(m => m.ChannelId == channelId && m.UserId == targetUser.Id);

            if (targetIsMod && moderatorId != channel.OwnerId)
                throw new InvalidOperationException("Only the channel owner can time out a moderator.");

            var timeout = new ChatTimeout
            {
                ChannelId = channelId,
                UserId = targetUser.Id,
                ModeratorId = moderatorId,
                Reason = dto.Reason,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(dto.DurationSeconds)
            };

            _context.ChatTimeouts.Add(timeout);
            await _context.SaveChangesAsync();

            var moderator = await _userManager.FindByIdAsync(moderatorId);

            _logger.LogInformation(
                "User '{Username}' timed out for {Duration}s in channel {ChannelId} by {ModeratorId}.",
                dto.Username, dto.DurationSeconds, channelId, moderatorId);

            return new ModerationActionDto
            {
                Action = "Timeout",
                TargetUsername = dto.Username,
                ModeratorName = moderator?.FullName ?? "Unknown",
                Reason = dto.Reason,
                DurationSeconds = dto.DurationSeconds,
                Timestamp = DateTime.UtcNow,
                Message = $"User '{dto.Username}' has been timed out for {dto.DurationSeconds} seconds."
            };
        }

        public async Task<ModerationActionDto> BanUserAsync(int channelId, string moderatorId, BanUserDto dto)
        {
            await EnsureModerationPrivileges(channelId, moderatorId);

            var targetUser = await _userManager.FindByNameAsync(dto.Username)
                ?? throw new InvalidOperationException($"User '{dto.Username}' not found.");

            var channel = await _context.Channels.FindAsync(channelId)
                ?? throw new InvalidOperationException("Channel not found.");

            if (targetUser.Id == channel.OwnerId)
                throw new InvalidOperationException("You cannot ban the channel owner.");

            // Prevent banning other moderators (only the owner can)
            var targetIsMod = await _context.ChannelModerators
                .AnyAsync(m => m.ChannelId == channelId && m.UserId == targetUser.Id);

            if (targetIsMod && moderatorId != channel.OwnerId)
                throw new InvalidOperationException("Only the channel owner can ban a moderator.");

            // Check if already banned
            var existingBan = await _context.ChatBans
                .FirstOrDefaultAsync(b => b.ChannelId == channelId && b.UserId == targetUser.Id && b.IsActive);

            if (existingBan != null)
                throw new InvalidOperationException($"User '{dto.Username}' is already banned from this channel.");

            var ban = new ChatBan
            {
                ChannelId = channelId,
                UserId = targetUser.Id,
                ModeratorId = moderatorId,
                Reason = dto.Reason,
                BannedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.ChatBans.Add(ban);
            await _context.SaveChangesAsync();

            var moderator = await _userManager.FindByIdAsync(moderatorId);

            _logger.LogInformation(
                "User '{Username}' banned from channel {ChannelId} by {ModeratorId}.",
                dto.Username, channelId, moderatorId);

            return new ModerationActionDto
            {
                Action = "Ban",
                TargetUsername = dto.Username,
                ModeratorName = moderator?.FullName ?? "Unknown",
                Reason = dto.Reason,
                Timestamp = DateTime.UtcNow,
                Message = $"User '{dto.Username}' has been banned from this channel."
            };
        }

        public async Task<ModerationActionDto> UnbanUserAsync(int channelId, string moderatorId, string username)
        {
            await EnsureModerationPrivileges(channelId, moderatorId);

            var targetUser = await _userManager.FindByNameAsync(username)
                ?? throw new InvalidOperationException($"User '{username}' not found.");

            var ban = await _context.ChatBans
                .FirstOrDefaultAsync(b => b.ChannelId == channelId && b.UserId == targetUser.Id && b.IsActive)
                ?? throw new InvalidOperationException($"User '{username}' is not banned from this channel.");

            ban.IsActive = false;
            await _context.SaveChangesAsync();

            var moderator = await _userManager.FindByIdAsync(moderatorId);

            _logger.LogInformation(
                "User '{Username}' unbanned from channel {ChannelId} by {ModeratorId}.",
                username, channelId, moderatorId);

            return new ModerationActionDto
            {
                Action = "Unban",
                TargetUsername = username,
                ModeratorName = moderator?.FullName ?? "Unknown",
                Timestamp = DateTime.UtcNow,
                Message = $"User '{username}' has been unbanned from this channel."
            };
        }

        public async Task<bool> IsUserTimedOutAsync(int channelId, string userId)
        {
            return await _context.ChatTimeouts
                .AnyAsync(t => t.ChannelId == channelId
                            && t.UserId == userId
                            && t.ExpiresAt > DateTime.UtcNow);
        }

        public async Task<bool> IsUserBannedAsync(int channelId, string userId)
        {
            return await _context.ChatBans
                .AnyAsync(b => b.ChannelId == channelId
                            && b.UserId == userId
                            && b.IsActive);
        }

        public async Task<bool> HasModerationPrivilegesAsync(int channelId, string userId)
        {
            // Channel owner always has mod privileges
            var isOwner = await _context.Channels
                .AnyAsync(c => c.Id == channelId && c.OwnerId == userId);

            if (isOwner) return true;

            // Check if the user is a hired moderator for this channel
            return await _context.ChannelModerators
                .AnyAsync(m => m.ChannelId == channelId && m.UserId == userId);
        }

        private async Task EnsureModerationPrivileges(int channelId, string userId)
        {
            var hasPrivileges = await HasModerationPrivilegesAsync(channelId, userId);
            if (!hasPrivileges)
                throw new InvalidOperationException("You do not have moderation privileges for this channel.");
        }
    }
}
