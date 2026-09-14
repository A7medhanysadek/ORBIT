using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Notification;
using OrbitBackend.Hubs;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<StreamChatHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            AppDbContext context,
            IHubContext<StreamChatHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, int count = 30)
        {
            count = Math.Clamp(count, 1, 100);

            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Type = n.Type,
                    Title = n.Title,
                    Message = n.Message,
                    Data = n.Data,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);
        }

        public async Task<bool> MarkAsReadAsync(string userId, int notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

            if (notification == null)
                return false;

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unread.Count > 0)
            {
                foreach (var n in unread)
                {
                    n.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }
        }

        public async Task CreateNotificationAsync(string userId, string type, string title, string message, string? data = null)
        {
            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                Data = data,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            var dto = new NotificationDto
            {
                Id = notification.Id,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                Data = notification.Data,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            };

            // Broadcast real-time event to user's personal SignalR group
            try
            {
                await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send real-time notification to user {UserId}", userId);
            }
        }

        public async Task NotifyFollowersStreamLiveAsync(int streamId, int channelId, string channelName, string streamTitle)
        {
            var followerUserIds = await _context.ChannelFollows
                .Where(f => f.ChannelId == channelId)
                .Select(f => f.UserId)
                .ToListAsync();

            if (followerUserIds.Count == 0)
                return;

            var notifications = followerUserIds.Select(uid => new Notification
            {
                UserId = uid,
                Type = "STREAM_LIVE",
                Title = $"{channelName} is LIVE!",
                Message = string.IsNullOrWhiteSpace(streamTitle) ? "Broadcast has started." : streamTitle,
                Data = streamId.ToString(),
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            // Attempt real-time dispatch for online followers
            foreach (var n in notifications)
            {
                try
                {
                    await _hubContext.Clients.User(n.UserId).SendAsync("ReceiveNotification", new NotificationDto
                    {
                        Id = n.Id,
                        Type = n.Type,
                        Title = n.Title,
                        Message = n.Message,
                        Data = n.Data,
                        IsRead = false,
                        CreatedAt = n.CreatedAt
                    });
                }
                catch
                {
                    // Ignore offline SignalR dispatches
                }
            }
        }

        public async Task NotifyNewFollowerAsync(string channelOwnerId, string followerUsername, int channelId)
        {
            await CreateNotificationAsync(
                channelOwnerId,
                "NEW_FOLLOWER",
                "New Follower!",
                $"@{followerUsername} started following your channel.",
                channelId.ToString());
        }

        public async Task NotifyModeratorHiredAsync(string moderatorUserId, string channelName, int channelId)
        {
            await CreateNotificationAsync(
                moderatorUserId,
                "MOD_HIRED",
                "Moderator Badge Assigned",
                $"You were appointed as a channel moderator for {channelName}.",
                channelId.ToString());
        }
    }
}
