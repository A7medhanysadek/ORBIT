using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Moderation;
using OrbitBackend.Hubs;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class ModerationService : IModerationService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IHubContext<StreamChatHub> _hubContext;
        private readonly ILogger<ModerationService> _logger;

        public ModerationService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IConfiguration config,
            IHubContext<StreamChatHub> hubContext,
            ILogger<ModerationService> logger)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _hubContext = hubContext;
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

            try
            {
                await _hubContext.Clients.Group($"stream_{message.LiveStreamId}").SendAsync("MessageDeleted", messageId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast MessageDeleted via SignalR for stream {StreamId}", message.LiveStreamId);
            }

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

            var targetUser = await FindTargetUserAsync(dto.Username)
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

            try
            {
                var liveStreamIds = await _context.LiveStreams
                    .Where(s => s.ChannelId == channelId && s.IsLive)
                    .Select(s => s.Id)
                    .ToListAsync();

                foreach (var sid in liveStreamIds)
                {
                    await _hubContext.Clients.Group($"stream_{sid}").SendAsync("UserTimedOut", targetUser.UserName ?? dto.Username, dto.DurationSeconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast UserTimedOut via SignalR for channel {ChannelId}", channelId);
            }

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

            var targetUser = await FindTargetUserAsync(dto.Username)
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

            try
            {
                var liveStreamIds = await _context.LiveStreams
                    .Where(s => s.ChannelId == channelId && s.IsLive)
                    .Select(s => s.Id)
                    .ToListAsync();

                foreach (var sid in liveStreamIds)
                {
                    await _hubContext.Clients.Group($"stream_{sid}").SendAsync("UserBanned", targetUser.UserName ?? dto.Username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast UserBanned via SignalR for channel {ChannelId}", channelId);
            }

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

            var targetUser = await FindTargetUserAsync(username)
                ?? throw new InvalidOperationException($"User '{username}' not found.");

            var ban = await _context.ChatBans
                .FirstOrDefaultAsync(b => b.ChannelId == channelId && b.UserId == targetUser.Id && b.IsActive)
                ?? throw new InvalidOperationException($"User '{username}' is not banned from this channel.");

            ban.IsActive = false;
            await _context.SaveChangesAsync();

            try
            {
                var liveStreamIds = await _context.LiveStreams
                    .Where(s => s.ChannelId == channelId && s.IsLive)
                    .Select(s => s.Id)
                    .ToListAsync();

                foreach (var sid in liveStreamIds)
                {
                    await _hubContext.Clients.Group($"stream_{sid}").SendAsync("UserUnbanned", targetUser.UserName ?? username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast UserUnbanned via SignalR for channel {ChannelId}", channelId);
            }

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

        public async Task<ModerationActionDto> RemoveTimeoutAsync(int channelId, string moderatorId, string username)
        {
            await EnsureModerationPrivileges(channelId, moderatorId);

            var targetUser = await FindTargetUserAsync(username)
                ?? throw new InvalidOperationException($"User '{username}' not found.");

            var activeTimeouts = await _context.ChatTimeouts
                .Where(t => t.ChannelId == channelId && t.UserId == targetUser.Id && t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            if (!activeTimeouts.Any())
                throw new InvalidOperationException($"User '{username}' is not timed out in this channel.");

            foreach (var t in activeTimeouts)
            {
                t.ExpiresAt = DateTime.UtcNow;
            }
            await _context.SaveChangesAsync();

            try
            {
                var liveStreamIds = await _context.LiveStreams
                    .Where(s => s.ChannelId == channelId && s.IsLive)
                    .Select(s => s.Id)
                    .ToListAsync();

                foreach (var sid in liveStreamIds)
                {
                    await _hubContext.Clients.Group($"stream_{sid}").SendAsync("UserTimeoutRemoved", targetUser.UserName ?? username);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast UserTimeoutRemoved via SignalR for channel {ChannelId}", channelId);
            }

            var moderator = await _userManager.FindByIdAsync(moderatorId);

            _logger.LogInformation(
                "Timeout for user '{Username}' removed in channel {ChannelId} by {ModeratorId}.",
                username, channelId, moderatorId);

            return new ModerationActionDto
            {
                Action = "RemoveTimeout",
                TargetUsername = username,
                ModeratorName = moderator?.FullName ?? "Unknown",
                Timestamp = DateTime.UtcNow,
                Message = $"Timeout for user '{username}' has been removed."
            };
        }

        public async Task<UserModerationStatusDto> GetUserModerationStatusAsync(int channelId, string username)
        {
            var targetUser = await FindTargetUserAsync(username);
            if (targetUser == null)
            {
                return new UserModerationStatusDto
                {
                    Username = username,
                    IsModerator = false,
                    IsTimedOut = false,
                    IsBanned = false
                };
            }

            var isMod = await _context.ChannelModerators
                .AnyAsync(m => m.ChannelId == channelId && m.UserId == targetUser.Id);

            var isBanned = await _context.ChatBans
                .AnyAsync(b => b.ChannelId == channelId && b.UserId == targetUser.Id && b.IsActive);

            var activeTimeout = await _context.ChatTimeouts
                .Where(t => t.ChannelId == channelId && t.UserId == targetUser.Id && t.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(t => t.ExpiresAt)
                .FirstOrDefaultAsync();

            return new UserModerationStatusDto
            {
                Username = targetUser.UserName ?? username,
                UserId = targetUser.Id,
                DisplayName = targetUser.FullName,
                IsModerator = isMod,
                IsBanned = isBanned,
                IsTimedOut = activeTimeout != null,
                TimeoutRemainingSeconds = activeTimeout != null
                    ? (int)Math.Max(0, (activeTimeout.ExpiresAt - DateTime.UtcNow).TotalSeconds)
                    : 0
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
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin")) return true;

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

        private async Task<AppUser?> FindTargetUserAsync(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return null;

            var trimmed = identifier.Trim();

            // 1. Exact handle via Identity UserManager
            var user = await _userManager.FindByNameAsync(trimmed);
            if (user != null) return user;

            // 2. User Id lookup
            user = await _userManager.FindByIdAsync(trimmed);
            if (user != null) return user;

            // 3. Email lookup
            user = await _userManager.FindByEmailAsync(trimmed);
            if (user != null) return user;

            // 4. Case-insensitive search on UserName or FullName
            var lower = trimmed.ToLower();
            user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    (u.UserName != null && u.UserName.ToLower() == lower) ||
                    (u.FullName != null && u.FullName.ToLower() == lower));
            if (user != null) return user;

            // 5. Normalized match fallback
            var upper = trimmed.ToUpper();
            return await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.NormalizedUserName == upper ||
                    u.NormalizedEmail == upper);
        }
    }
}
