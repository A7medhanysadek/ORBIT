using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Channel;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class ChannelService : IChannelService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly ViewerTracker _viewerTracker;
        private readonly ILogger<ChannelService> _logger;
        private readonly INotificationService _notificationService;

        public ChannelService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ICloudinaryService cloudinaryService,
            ViewerTracker viewerTracker,
            ILogger<ChannelService> logger,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _cloudinaryService = cloudinaryService;
            _viewerTracker = viewerTracker;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<ChannelResponseDto> CreateChannelAsync(string userId, CreateChannelDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            // Check if user already has a channel
            var existingChannel = await _context.Channels
                .AnyAsync(c => c.OwnerId == userId);

            if (existingChannel)
                throw new InvalidOperationException("You already have a channel. Each user can only create one channel.");

            // Check if channel name is taken
            var nameTaken = await _context.Channels
                .AnyAsync(c => c.ChannelName == dto.ChannelName);

            if (nameTaken)
                throw new InvalidOperationException($"The channel name '{dto.ChannelName}' is already taken.");

            var channel = new Channel
            {
                ChannelName = dto.ChannelName,
                Description = dto.Description,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Channels.Add(channel);
            await _context.SaveChangesAsync();

            // Automatically grant the Streamer role
            if (!await _userManager.IsInRoleAsync(user, "Streamer"))
            {
                await _userManager.AddToRoleAsync(user, "Streamer");
            }

            _logger.LogInformation(
                "Channel '{ChannelName}' (ID: {ChannelId}) created by user {UserId}. Streamer role granted.",
                channel.ChannelName, channel.Id, userId);

            return MapToResponseDto(channel, user);
        }

        public async Task<ChannelResponseDto> GetChannelByIdAsync(int channelId)
        {
            var channel = await _context.Channels
                .Include(c => c.Owner)
                .Include(c => c.Moderators)
                .Include(c => c.LiveStreams)
                .Include(c => c.SocialLinks)
                .Include(c => c.Followers)
                .FirstOrDefaultAsync(c => c.Id == channelId)
                ?? throw new InvalidOperationException("Channel not found.");

            return MapToResponseDto(channel, channel.Owner);
        }

        public async Task<ChannelResponseDto> GetMyChannelAsync(string userId)
        {
            var channel = await _context.Channels
                .Include(c => c.Owner)
                .Include(c => c.Moderators)
                .Include(c => c.LiveStreams)
                .Include(c => c.SocialLinks)
                .Include(c => c.Followers)
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You don't have a channel. Create one first using POST /api/channel/create.");

            return MapToResponseDto(channel, channel.Owner);
        }

        public async Task<ChannelModeratorDto> HireModeratorAsync(string ownerUserId, HireModeratorDto dto)
        {
            var caller = await _userManager.FindByIdAsync(ownerUserId);
            var isAdmin = caller != null && await _userManager.IsInRoleAsync(caller, "Admin");

            Channel? channel = null;
            if (dto.ChannelId.HasValue && dto.ChannelId.Value > 0)
            {
                channel = await _context.Channels
                    .Include(c => c.Moderators)
                    .FirstOrDefaultAsync(c => c.Id == dto.ChannelId.Value && (c.OwnerId == ownerUserId || isAdmin));
            }

            if (channel == null)
            {
                channel = await _context.Channels
                    .Include(c => c.Moderators)
                    .FirstOrDefaultAsync(c => c.OwnerId == ownerUserId);
            }

            if (channel == null)
                throw new InvalidOperationException("You don't have permission to manage moderators for this channel or you don't have a channel.");

            var targetUser = await FindTargetUserAsync(dto.Username)
                ?? throw new InvalidOperationException($"User '{dto.Username}' not found.");

            if (targetUser.Id == channel.OwnerId)
                throw new InvalidOperationException("You cannot hire the channel owner as a moderator.");

            // Check if already a moderator
            var alreadyMod = channel.Moderators.Any(m => m.UserId == targetUser.Id);
            if (alreadyMod)
                throw new InvalidOperationException($"'{targetUser.FullName ?? targetUser.UserName}' is already a moderator for this channel.");

            var moderator = new ChannelModerator
            {
                ChannelId = channel.Id,
                UserId = targetUser.Id,
                HiredAt = DateTime.UtcNow
            };

            _context.ChannelModerators.Add(moderator);
            await _context.SaveChangesAsync();

            // Grant the Moderator role if not already assigned
            if (!await _userManager.IsInRoleAsync(targetUser, "Moderator"))
            {
                await _userManager.AddToRoleAsync(targetUser, "Moderator");
            }

            // Notify user that they were hired as a moderator
            try
            {
                await _notificationService.NotifyModeratorHiredAsync(targetUser.Id, channel.ChannelName, channel.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send mod notification to user {UserId}", targetUser.Id);
            }

            _logger.LogInformation(
                "User '{Username}' hired as moderator for channel '{ChannelName}'.",
                dto.Username, channel.ChannelName);

            return new ChannelModeratorDto
            {
                UserId = targetUser.Id,
                Username = targetUser.UserName!,
                FullName = targetUser.FullName,
                HiredAt = moderator.HiredAt
            };
        }

        public async Task RemoveModeratorAsync(string ownerUserId, string username, int? channelId = null)
        {
            var caller = await _userManager.FindByIdAsync(ownerUserId);
            var isAdmin = caller != null && await _userManager.IsInRoleAsync(caller, "Admin");

            Channel? channel = null;
            if (channelId.HasValue && channelId.Value > 0)
            {
                channel = await _context.Channels
                    .FirstOrDefaultAsync(c => c.Id == channelId.Value && (c.OwnerId == ownerUserId || isAdmin));
            }

            if (channel == null)
            {
                channel = await _context.Channels
                    .FirstOrDefaultAsync(c => c.OwnerId == ownerUserId);
            }

            if (channel == null)
                throw new InvalidOperationException("You don't have permission to manage moderators for this channel or you don't have a channel.");

            var targetUser = await FindTargetUserAsync(username)
                ?? throw new InvalidOperationException($"User '{username}' not found.");

            var moderator = await _context.ChannelModerators
                .FirstOrDefaultAsync(m => m.ChannelId == channel.Id && m.UserId == targetUser.Id)
                ?? throw new InvalidOperationException($"'{targetUser.FullName ?? targetUser.UserName}' is not a moderator for this channel.");

            _context.ChannelModerators.Remove(moderator);
            await _context.SaveChangesAsync();

            // Remove Moderator role if the user doesn't moderate any other channel
            var stillModeratesOtherChannels = await _context.ChannelModerators
                .AnyAsync(m => m.UserId == targetUser.Id);

            if (!stillModeratesOtherChannels)
            {
                await _userManager.RemoveFromRoleAsync(targetUser, "Moderator");
            }

            _logger.LogInformation(
                "User '{Username}' removed as moderator from channel '{ChannelName}'.",
                username, channel.ChannelName);
        }

        public async Task<List<ChannelModeratorDto>> GetModeratorsAsync(string ownerUserId, int? channelId = null)
        {
            var caller = await _userManager.FindByIdAsync(ownerUserId);
            var isAdmin = caller != null && await _userManager.IsInRoleAsync(caller, "Admin");

            Channel? channel = null;
            if (channelId.HasValue && channelId.Value > 0)
            {
                channel = await _context.Channels
                    .FirstOrDefaultAsync(c => c.Id == channelId.Value && (c.OwnerId == ownerUserId || isAdmin));
            }

            if (channel == null)
            {
                channel = await _context.Channels
                    .FirstOrDefaultAsync(c => c.OwnerId == ownerUserId);
            }

            if (channel == null)
                throw new InvalidOperationException("You don't have a channel.");

            return await _context.ChannelModerators
                .Where(m => m.ChannelId == channel.Id)
                .Include(m => m.User)
                .Select(m => new ChannelModeratorDto
                {
                    UserId = m.UserId,
                    Username = m.User.UserName!,
                    FullName = m.User.FullName,
                    HiredAt = m.HiredAt
                })
                .ToListAsync();
        }

        // ── Channel Customization ──

        public async Task<ChannelResponseDto> UpdateChannelProfileAsync(string userId, UpdateChannelProfileDto dto)
        {
            var channel = await _context.Channels
                .Include(c => c.Owner)
                .Include(c => c.Moderators)
                .Include(c => c.LiveStreams)
                .Include(c => c.SocialLinks)
                .Include(c => c.Followers)
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You don't have a channel.");

            if (dto.Description != null)
                channel.Description = dto.Description;

            if (dto.DonationUrl != null)
                channel.DonationUrl = dto.DonationUrl;

            if (dto.DonationMessage != null)
                channel.DonationMessage = dto.DonationMessage;

            if (dto.SaveStreams.HasValue)
                channel.SaveStreams = dto.SaveStreams.Value;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Channel {ChannelId} profile updated by user {UserId}.", channel.Id, userId);

            return MapToResponseDto(channel, channel.Owner);
        }

        public async Task<ChannelResponseDto> UploadChannelPhotoAsync(string userId, IFormFile file)
        {
            var channel = await _context.Channels
                .Include(c => c.Owner)
                .Include(c => c.Moderators)
                .Include(c => c.LiveStreams)
                .Include(c => c.SocialLinks)
                .Include(c => c.Followers)
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You don't have a channel.");

            // Delete old photo if exists
            if (!string.IsNullOrEmpty(channel.ProfilePhotoUrl))
            {
                var oldPublicId = _cloudinaryService.GetPublicIdFromUrl(channel.ProfilePhotoUrl);
                if (!string.IsNullOrEmpty(oldPublicId))
                    await _cloudinaryService.DeleteImageAsync(oldPublicId);
            }

            var url = await _cloudinaryService.UploadImageAsync(file, "orbit/channel-photos");
            channel.ProfilePhotoUrl = url;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Channel {ChannelId} profile photo updated.", channel.Id);

            return MapToResponseDto(channel, channel.Owner);
        }

        public async Task<ChannelResponseDto> UploadChannelCoverAsync(string userId, IFormFile file)
        {
            var channel = await _context.Channels
                .Include(c => c.Owner)
                .Include(c => c.Moderators)
                .Include(c => c.LiveStreams)
                .Include(c => c.SocialLinks)
                .Include(c => c.Followers)
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You don't have a channel.");

            // Delete old cover if exists
            if (!string.IsNullOrEmpty(channel.CoverPhotoUrl))
            {
                var oldPublicId = _cloudinaryService.GetPublicIdFromUrl(channel.CoverPhotoUrl);
                if (!string.IsNullOrEmpty(oldPublicId))
                    await _cloudinaryService.DeleteImageAsync(oldPublicId);
            }

            var url = await _cloudinaryService.UploadImageAsync(file, "orbit/channel-covers");
            channel.CoverPhotoUrl = url;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Channel {ChannelId} cover photo updated.", channel.Id);

            return MapToResponseDto(channel, channel.Owner);
        }

        public async Task<List<ChannelSocialLinkDto>> UpdateSocialLinksAsync(string userId, UpdateChannelSocialLinksDto dto)
        {
            var channel = await _context.Channels
                .Include(c => c.SocialLinks)
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You don't have a channel.");

            // Remove existing links
            _context.ChannelSocialLinks.RemoveRange(channel.SocialLinks);

            // Add new links
            var newLinks = dto.SocialLinks.Select(sl => new ChannelSocialLink
            {
                ChannelId = channel.Id,
                Platform = sl.Platform.ToLower().Trim(),
                Url = sl.Url.Trim()
            }).ToList();

            _context.ChannelSocialLinks.AddRange(newLinks);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Channel {ChannelId} social links updated ({Count} links).", channel.Id, newLinks.Count);

            return newLinks.Select(sl => new ChannelSocialLinkDto
            {
                Platform = sl.Platform,
                Url = sl.Url
            }).ToList();
        }

        public async Task<List<ChannelSocialLinkDto>> GetSocialLinksAsync(int channelId)
        {
            var exists = await _context.Channels.AnyAsync(c => c.Id == channelId);
            if (!exists)
                throw new InvalidOperationException("Channel not found.");

            return await _context.ChannelSocialLinks
                .Where(sl => sl.ChannelId == channelId)
                .Select(sl => new ChannelSocialLinkDto
                {
                    Platform = sl.Platform,
                    Url = sl.Url
                })
                .ToListAsync();
        }

        public async Task<List<OrbitBackend.DTOs.Admin.ChannelSearchResultDto>> SearchChannelsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<OrbitBackend.DTOs.Admin.ChannelSearchResultDto>();

            var trimmed = query.Trim().ToLower();

            var channels = await _context.Channels
                .Include(c => c.Owner)
                .Include(c => c.LiveStreams)
                    .ThenInclude(s => s.Category)
                .Where(c =>
                    c.ChannelName.ToLower().Contains(trimmed) ||
                    (c.Description != null && c.Description.ToLower().Contains(trimmed)) ||
                    (c.Owner != null && c.Owner.UserName != null && c.Owner.UserName.ToLower().Contains(trimmed)) ||
                    (c.Owner != null && c.Owner.FullName != null && c.Owner.FullName.ToLower().Contains(trimmed)))
                .Take(30)
                .AsNoTracking()
                .ToListAsync();

            return channels.Select(c =>
            {
                var activeStream = c.LiveStreams.FirstOrDefault(s => s.IsLive);
                return new OrbitBackend.DTOs.Admin.ChannelSearchResultDto
                {
                    Id = c.Id,
                    ChannelName = c.ChannelName,
                    Description = c.Description,
                    ProfilePhotoUrl = c.ProfilePhotoUrl,
                    OwnerUsername = c.Owner?.UserName ?? c.Owner?.FullName ?? string.Empty,
                    IsLive = activeStream != null,
                    ViewerCount = activeStream != null ? _viewerTracker.GetViewerCount(activeStream.Id) : 0,
                    CategoryName = activeStream?.Category?.Name
                };
            }).ToList();
        }

        // ── Following ──

        public async Task<bool> ToggleFollowAsync(string userId, int channelId)
        {
            var channel = await _context.Channels.FirstOrDefaultAsync(c => c.Id == channelId)
                ?? throw new InvalidOperationException("Channel not found.");

            if (channel.OwnerId == userId)
                throw new InvalidOperationException("You cannot follow your own channel.");

            var existingFollow = await _context.ChannelFollows
                .FirstOrDefaultAsync(f => f.ChannelId == channelId && f.UserId == userId);

            if (existingFollow != null)
            {
                _context.ChannelFollows.Remove(existingFollow);
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {UserId} unfollowed channel {ChannelId}.", userId, channelId);
                return false;
            }

            var follow = new ChannelFollow
            {
                ChannelId = channelId,
                UserId = userId,
                FollowedAt = DateTime.UtcNow
            };

            _context.ChannelFollows.Add(follow);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User {UserId} followed channel {ChannelId}.", userId, channelId);

            // Notify channel owner of new follower
            try
            {
                var followerUser = await _userManager.FindByIdAsync(userId);
                if (followerUser != null)
                {
                    await _notificationService.NotifyNewFollowerAsync(channel.OwnerId, followerUser.UserName ?? followerUser.FullName, channel.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send new follower notification to channel owner {OwnerId}", channel.OwnerId);
            }

            return true;
        }

        public async Task<bool> IsFollowingAsync(string userId, int channelId)
        {
            return await _context.ChannelFollows
                .AnyAsync(f => f.ChannelId == channelId && f.UserId == userId);
        }

        public async Task<List<ChannelFollowDto>> GetFollowedChannelsAsync(string userId)
        {
            return await _context.ChannelFollows
                .Where(f => f.UserId == userId)
                .Include(f => f.Channel)
                    .ThenInclude(c => c.Owner)
                .Include(f => f.Channel)
                    .ThenInclude(c => c.LiveStreams)
                .OrderByDescending(f => f.FollowedAt)
                .Select(f => new ChannelFollowDto
                {
                    ChannelId = f.ChannelId,
                    ChannelName = f.Channel.ChannelName,
                    OwnerUsername = f.Channel.Owner.UserName ?? f.Channel.Owner.FullName ?? string.Empty,
                    ProfilePhotoUrl = f.Channel.ProfilePhotoUrl,
                    IsLive = f.Channel.LiveStreams.Any(s => s.IsLive),
                    FollowedAt = f.FollowedAt
                })
                .ToListAsync();
        }

        public async Task<List<OrbitBackend.DTOs.Dashboard.CustomEmojiResponseDto>> GetChannelEmojisAsync(int channelId)
        {
            return await _context.ChannelEmojis
                .AsNoTracking()
                .Where(e => e.ChannelId == channelId)
                .OrderBy(e => e.Name)
                .Select(e => new OrbitBackend.DTOs.Dashboard.CustomEmojiResponseDto
                {
                    Id = e.Id,
                    Name = e.Name,
                    EmojiValue = e.EmojiValue,
                    IsCustomImage = e.IsCustomImage,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();
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

        private static ChannelResponseDto MapToResponseDto(Channel channel, AppUser owner)
        {
            return new ChannelResponseDto
            {
                Id = channel.Id,
                ChannelName = channel.ChannelName,
                Description = channel.Description,
                OwnerId = owner.Id,
                OwnerName = owner.FullName,
                OwnerProfilePictureUrl = owner.ProfilePictureUrl,
                CreatedAt = channel.CreatedAt,
                IsLive = channel.LiveStreams?.Any(s => s.IsLive) ?? false,
                ModeratorCount = channel.Moderators?.Count ?? 0,
                ProfilePhotoUrl = channel.ProfilePhotoUrl,
                CoverPhotoUrl = channel.CoverPhotoUrl,
                DonationUrl = channel.DonationUrl,
                DonationMessage = channel.DonationMessage,
                SaveStreams = channel.SaveStreams,
                FollowerCount = channel.Followers?.Count ?? 0,
                SocialLinks = channel.SocialLinks?.Select(sl => new ChannelSocialLinkDto
                {
                    Platform = sl.Platform,
                    Url = sl.Url
                }).ToList() ?? new()
            };
        }
    }
}
