using System.Security.Cryptography;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Admin;
using OrbitBackend.DTOs.Clip;
using OrbitBackend.DTOs.Common;
using OrbitBackend.DTOs.Vod;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class AdminService : IAdminService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ViewerTracker _viewerTracker;
        private readonly IMapper _mapper;

        public AdminService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ViewerTracker viewerTracker,
            IMapper mapper)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _viewerTracker = viewerTracker;
            _mapper = mapper;
        }

        public async Task<AdminStatsDto> GetSystemStatsAsync()
        {
            var totalUsers = await _userManager.Users.CountAsync();
            var totalChannels = await _context.Channels.CountAsync();
            var activeStreams = await _context.LiveStreams.CountAsync(s => s.IsLive);
            var totalClips = await _context.Clips.CountAsync();
            var totalVods = await _context.LiveStreams.CountAsync(s => !s.IsLive && !string.IsNullOrEmpty(s.RecordingFileName));
            var totalCategories = await _context.Categories.CountAsync();
            var totalChatMessages = await _context.ChatMessages.CountAsync();

            return new AdminStatsDto
            {
                TotalUsers = totalUsers,
                TotalChannels = totalChannels,
                ActiveStreams = activeStreams,
                TotalClips = totalClips,
                TotalVods = totalVods,
                TotalCategories = totalCategories,
                TotalChatMessages = totalChatMessages
            };
        }

        public async Task<PaginatedResponseDto<AdminUserDto>> GetUsersAsync(int page, int pageSize, string? search = null, string? role = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _userManager.Users
                .Include(u => u.Channel)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.ToLower().Contains(search)) ||
                    (u.Email != null && u.Email.ToLower().Contains(search)) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(search)));
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<AdminUserDto>();
            foreach (var user in users)
            {
                var userRoles = (await _userManager.GetRolesAsync(user)).ToList();

                if (!string.IsNullOrWhiteSpace(role) && !userRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new AdminUserDto
                {
                    Id = user.Id,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName ?? string.Empty,
                    Age = user.Age,
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    Roles = userRoles,
                    IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    LockoutEnd = user.LockoutEnd,
                    HasChannel = user.Channel != null,
                    ChannelId = user.Channel?.Id,
                    ChannelName = user.Channel?.ChannelName
                });
            }

            return new PaginatedResponseDto<AdminUserDto>
            {
                Items = result,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AdminUserDto> GetUserByIdAsync(string userId)
        {
            var user = await _userManager.Users
                .Include(u => u.Channel)
                .FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("User not found.");

            var userRoles = (await _userManager.GetRolesAsync(user)).ToList();

            return new AdminUserDto
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                Age = user.Age,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Roles = userRoles,
                IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                LockoutEnd = user.LockoutEnd,
                HasChannel = user.Channel != null,
                ChannelId = user.Channel?.Id,
                ChannelName = user.Channel?.ChannelName
            };
        }

        public async Task UpdateUserRolesAsync(string currentAdminId, string targetUserId, UpdateUserRolesDto dto)
        {
            var targetUser = await _userManager.FindByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("Target user not found.");

            var currentRoles = await _userManager.GetRolesAsync(targetUser);

            // Prevent an admin from removing their own Admin role
            if (string.Equals(currentAdminId, targetUserId, StringComparison.OrdinalIgnoreCase))
            {
                if (currentRoles.Contains("Admin") && !dto.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("You cannot remove your own Admin role.");
                }
            }

            // Ensure all requested roles exist
            foreach (var role in dto.Roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Remove roles no longer included
            var rolesToRemove = currentRoles.Except(dto.Roles, StringComparer.OrdinalIgnoreCase).ToList();
            if (rolesToRemove.Any())
            {
                await _userManager.RemoveFromRolesAsync(targetUser, rolesToRemove);
            }

            // Add new roles
            var rolesToAdd = dto.Roles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
            if (rolesToAdd.Any())
            {
                await _userManager.AddToRolesAsync(targetUser, rolesToAdd);
            }
        }

        public async Task LockUserAsync(string currentAdminId, string targetUserId, LockUserDto dto)
        {
            if (string.Equals(currentAdminId, targetUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("You cannot lock your own account.");
            }

            var targetUser = await _userManager.FindByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("Target user not found.");

            if (dto.IsLocked)
            {
                await _userManager.SetLockoutEnabledAsync(targetUser, true);
                var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(dto.LockoutMinutes > 0 ? dto.LockoutMinutes : 1440);
                await _userManager.SetLockoutEndDateAsync(targetUser, lockoutEnd);
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(targetUser, null);
            }
        }

        public async Task ResetUserPasswordAsync(string currentAdminId, string targetUserId, AdminResetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            {
                throw new ArgumentException("Password must be at least 6 characters long.");
            }

            var targetUser = await _userManager.FindByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("Target user not found.");

            await _userManager.RemovePasswordAsync(targetUser);
            var addResult = await _userManager.AddPasswordAsync(targetUser, dto.NewPassword);

            if (!addResult.Succeeded)
            {
                var errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to set new password: {errors}");
            }
        }

        public async Task DeleteUserAsync(string currentAdminId, string targetUserId)
        {
            if (string.Equals(currentAdminId, targetUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("You cannot delete your own account.");
            }

            var targetUser = await _userManager.FindByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("Target user not found.");

            var result = await _userManager.DeleteAsync(targetUser);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to delete user: {errors}");
            }
        }

        public async Task<PaginatedResponseDto<AdminChannelDto>> GetChannelsAsync(int page, int pageSize, string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Channels
                .Include(c => c.Owner)
                .Include(c => c.LiveStreams)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(c =>
                    c.ChannelName.ToLower().Contains(search) ||
                    (c.Description != null && c.Description.ToLower().Contains(search)) ||
                    (c.Owner != null && c.Owner.UserName != null && c.Owner.UserName.ToLower().Contains(search)));
            }

            var totalCount = await query.CountAsync();

            var channels = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = channels.Select(c =>
            {
                var activeStream = c.LiveStreams.FirstOrDefault(s => s.IsLive);
                return new AdminChannelDto
                {
                    Id = c.Id,
                    ChannelName = c.ChannelName,
                    Description = c.Description,
                    ProfilePhotoUrl = c.ProfilePhotoUrl,
                    CoverPhotoUrl = c.CoverPhotoUrl,
                    OwnerId = c.OwnerId,
                    OwnerUsername = c.Owner?.UserName ?? string.Empty,
                    OwnerEmail = c.Owner?.Email ?? string.Empty,
                    HasStreamKey = !string.IsNullOrEmpty(c.StreamKey),
                    IsLive = activeStream != null,
                    CurrentViewers = activeStream != null ? _viewerTracker.GetViewerCount(activeStream.Id) : 0,
                    CreatedAt = c.CreatedAt
                };
            }).ToList();

            return new PaginatedResponseDto<AdminChannelDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<string> ResetChannelStreamKeyAsync(int channelId)
        {
            var channel = await _context.Channels.FindAsync(channelId)
                ?? throw new KeyNotFoundException("Channel not found.");

            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            var newKey = Convert.ToBase64String(bytes)
                .Replace("/", "_")
                .Replace("+", "-")
                .Replace("=", "");

            channel.StreamKey = newKey;
            await _context.SaveChangesAsync();

            return newKey;
        }

        public async Task DeleteChannelAsync(int channelId)
        {
            var channel = await _context.Channels
                .Include(c => c.LiveStreams)
                .Include(c => c.Moderators)
                .Include(c => c.SocialLinks)
                .Include(c => c.Clips)
                .FirstOrDefaultAsync(c => c.Id == channelId)
                ?? throw new KeyNotFoundException("Channel not found.");

            _context.Channels.Remove(channel);
            await _context.SaveChangesAsync();
        }

        public async Task<List<AdminStreamDto>> GetLiveStreamsAsync()
        {
            var liveStreams = await _context.LiveStreams
                .Include(s => s.Channel)
                    .ThenInclude(c => c.Owner)
                .Include(s => s.Category)
                .Where(s => s.IsLive)
                .AsNoTracking()
                .ToListAsync();

            return liveStreams.Select(s => new AdminStreamDto
            {
                StreamId = s.Id,
                ChannelId = s.ChannelId,
                ChannelName = s.Channel?.ChannelName ?? "Unknown",
                StreamerName = s.Channel?.Owner?.UserName ?? s.Channel?.Owner?.FullName ?? "Unknown",
                Title = s.Title,
                ViewerCount = _viewerTracker.GetViewerCount(s.Id),
                StartedAt = s.StartedAt ?? s.CreatedAt,
                CategoryName = s.Category?.Name,
                ThumbnailUrl = s.ThumbnailUrl
            })
            .OrderByDescending(s => s.ViewerCount)
            .ToList();
        }

        public async Task ForceEndStreamAsync(int streamId)
        {
            var stream = await _context.LiveStreams.FindAsync(streamId)
                ?? throw new KeyNotFoundException("Live stream not found.");

            if (stream.IsLive)
            {
                stream.IsLive = false;
                stream.EndedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _viewerTracker.ClearStream(streamId);
            }
        }

        public async Task<PaginatedResponseDto<ClipResponseDto>> GetClipsAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Clips
                .Include(c => c.Creator)
                .Include(c => c.Channel)
                .Include(c => c.Category)
                .AsNoTracking();

            var totalCount = await query.CountAsync();

            var clips = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = clips.Select(c => new ClipResponseDto
            {
                Id = c.Id,
                Title = c.Title,
                VideoUrl = c.VideoUrl,
                ThumbnailUrl = c.ThumbnailUrl,
                DurationSeconds = c.DurationSeconds,
                ViewCount = c.ViewCount,
                CreatedAt = c.CreatedAt,
                CreatorId = c.CreatorId,
                CreatorName = c.Creator?.UserName ?? "Unknown",
                ChannelId = c.ChannelId,
                ChannelName = c.Channel?.ChannelName ?? "Unknown",
                LiveStreamId = c.LiveStreamId,
                CategoryId = c.CategoryId,
                CategoryName = c.Category?.Name,
                CategorySlug = c.Category?.Slug
            }).ToList();

            return new PaginatedResponseDto<ClipResponseDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task DeleteClipAsync(int clipId)
        {
            var clip = await _context.Clips.FindAsync(clipId)
                ?? throw new KeyNotFoundException("Clip not found.");

            _context.Clips.Remove(clip);
            await _context.SaveChangesAsync();
        }

        public async Task<PaginatedResponseDto<SavedLiveDto>> GetVodsAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var query = _context.LiveStreams
                .Include(s => s.Category)
                .Include(s => s.ChatMessages)
                .Where(s => !s.IsLive)
                .AsNoTracking();

            var totalCount = await query.CountAsync();

            var vods = await query
                .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var dtos = vods.Select(s => new SavedLiveDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                ThumbnailUrl = s.ThumbnailUrl,
                VodUrl = s.RecordingFileName,
                CategoryName = s.Category?.Name,
                CategorySlug = s.Category?.Slug,
                DurationSeconds = (s.EndedAt.HasValue && s.StartedAt.HasValue) ? (s.EndedAt.Value - s.StartedAt.Value).TotalSeconds : null,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                ChatMessageCount = s.ChatMessages.Count
            }).ToList();

            return new PaginatedResponseDto<SavedLiveDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task DeleteVodAsync(int vodId)
        {
            var stream = await _context.LiveStreams.FindAsync(vodId)
                ?? throw new KeyNotFoundException("VOD not found.");

            stream.RecordingFileName = null;
            await _context.SaveChangesAsync();
        }
    }
}
