using OrbitBackend.DTOs.Notification;

namespace OrbitBackend.Services.Interfaces
{
    public interface INotificationService
    {
        Task<List<NotificationDto>> GetUserNotificationsAsync(string userId, int count = 30);
        Task<int> GetUnreadCountAsync(string userId);
        Task<bool> MarkAsReadAsync(string userId, int notificationId);
        Task MarkAllAsReadAsync(string userId);
        Task CreateNotificationAsync(string userId, string type, string title, string message, string? data = null);
        Task NotifyFollowersStreamLiveAsync(int streamId, int channelId, string channelName, string streamTitle);
        Task NotifyNewFollowerAsync(string channelOwnerId, string followerUsername, int channelId);
        Task NotifyModeratorHiredAsync(string moderatorUserId, string channelName, int channelId);
    }
}
