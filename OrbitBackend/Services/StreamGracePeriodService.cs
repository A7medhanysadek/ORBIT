using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.Hubs;

namespace OrbitBackend.Services
{
    /// <summary>
    /// Background service that periodically checks for streams that have been disconnected
    /// beyond the grace period and automatically ends them.
    /// 
    /// This enables stream reconnection: when OBS crashes or network drops,
    /// the stream stays "live" for the grace period. If the streamer reconnects
    /// within that window, the same stream session continues seamlessly.
    /// </summary>
    public class StreamGracePeriodService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ViewerTracker _viewerTracker;
        private readonly IHubContext<StreamChatHub> _hubContext;
        private readonly ILogger<StreamGracePeriodService> _logger;

        private const int CheckIntervalSeconds = 30;

        public StreamGracePeriodService(
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            ViewerTracker viewerTracker,
            IHubContext<StreamChatHub> hubContext,
            ILogger<StreamGracePeriodService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _viewerTracker = viewerTracker;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StreamGracePeriodService started. Checking every {Interval}s.", CheckIntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndEndExpiredStreamsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking for expired disconnected streams.");
                }

                await Task.Delay(TimeSpan.FromSeconds(CheckIntervalSeconds), stoppingToken);
            }
        }

        private async Task CheckAndEndExpiredStreamsAsync()
        {
            var gracePeriodSeconds = _config.GetValue("Streaming:ReconnectGracePeriodSeconds", 300);
            var cutoff = DateTime.UtcNow.AddSeconds(-gracePeriodSeconds);

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Find streams that are still marked live but disconnected beyond the grace period
            var expiredStreams = await context.LiveStreams
                .Include(s => s.Channel)
                .Where(s => s.IsLive
                         && s.DisconnectedAt != null
                         && s.DisconnectedAt <= cutoff)
                .ToListAsync();

            if (expiredStreams.Count == 0)
                return;

            foreach (var stream in expiredStreams)
            {
                stream.IsLive = false;
                stream.EndedAt = DateTime.UtcNow;

                // Record peak viewers
                var peak = _viewerTracker.GetPeakViewerCount(stream.Id);
                stream.PeakViewers = Math.Max(stream.PeakViewers, peak);

                // Auto-assign recording file if channel has SaveStreams enabled
                if (stream.Channel != null && stream.Channel.SaveStreams && string.IsNullOrEmpty(stream.RecordingFileName))
                {
                    stream.RecordingFileName = $"{stream.Channel.StreamKey}.flv";
                }

                // Notify viewers via SignalR
                try
                {
                    await _hubContext.Clients.Group($"stream_{stream.Id}").SendAsync("StreamEnded", stream.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast StreamEnded event for stream {StreamId}", stream.Id);
                }

                // Clean up viewer tracker
                _viewerTracker.ClearStream(stream.Id);

                _logger.LogInformation(
                    "Stream {StreamId} auto-ended after grace period expired. " +
                    "Disconnected at {DisconnectedAt}, grace period {GracePeriod}s, peak viewers {Peak}.",
                    stream.Id, stream.DisconnectedAt, gracePeriodSeconds, stream.PeakViewers);
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("{Count} stream(s) auto-ended after grace period expired.", expiredStreams.Count);
        }
    }
}
