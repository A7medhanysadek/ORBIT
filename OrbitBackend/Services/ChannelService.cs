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
        private readonly ILogger<ChannelService> _logger;

        public ChannelService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<ChannelService> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
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
                .FirstOrDefaultAsync(c => c.OwnerId == userId)
                ?? throw new InvalidOperationException("You don't have a channel. Create one first using POST /api/channel/create.");

            return MapToResponseDto(channel, channel.Owner);
        }

        public async Task<ChannelModeratorDto> HireModeratorAsync(string ownerUserId, HireModeratorDto dto)
        {
            var channel = await _context.Channels
                .Include(c => c.Moderators)
                .FirstOrDefaultAsync(c => c.OwnerId == ownerUserId)
                ?? throw new InvalidOperationException("You don't have a channel.");

            var targetUser = await _userManager.FindByNameAsync(dto.Username)
                ?? throw new InvalidOperationException($"User '{dto.Username}' not found.");

            if (targetUser.Id == ownerUserId)
                throw new InvalidOperationException("You cannot hire yourself as a moderator.");

            // Check if already a moderator
            var alreadyMod = channel.Moderators.Any(m => m.UserId == targetUser.Id);
            if (alreadyMod)
                throw new InvalidOperationException($"'{dto.Username}' is already a moderator for your channel.");

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

        public async Task RemoveModeratorAsync(string ownerUserId, string username)
        {
            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.OwnerId == ownerUserId)
                ?? throw new InvalidOperationException("You don't have a channel.");

            var targetUser = await _userManager.FindByNameAsync(username)
                ?? throw new InvalidOperationException($"User '{username}' not found.");

            var moderator = await _context.ChannelModerators
                .FirstOrDefaultAsync(m => m.ChannelId == channel.Id && m.UserId == targetUser.Id)
                ?? throw new InvalidOperationException($"'{username}' is not a moderator for your channel.");

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

        public async Task<List<ChannelModeratorDto>> GetModeratorsAsync(string ownerUserId)
        {
            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.OwnerId == ownerUserId)
                ?? throw new InvalidOperationException("You don't have a channel.");

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

        private static ChannelResponseDto MapToResponseDto(Channel channel, AppUser owner)
        {
            return new ChannelResponseDto
            {
                Id = channel.Id,
                ChannelName = channel.ChannelName,
                Description = channel.Description,
                OwnerId = owner.Id,
                OwnerName = owner.FullName,
                CreatedAt = channel.CreatedAt,
                IsLive = channel.LiveStreams?.Any(s => s.IsLive) ?? false,
                ModeratorCount = channel.Moderators?.Count ?? 0
            };
        }
    }
}
