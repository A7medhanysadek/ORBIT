using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;

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
        private readonly ILogger<StreamGracePeriodService> _logger;

        private const int CheckIntervalSeconds = 30;

        public StreamGracePeriodService(
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            ILogger<StreamGracePeriodService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
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

                _logger.LogInformation(
                    "Stream {StreamId} auto-ended after grace period expired. " +
                    "Disconnected at {DisconnectedAt}, grace period {GracePeriod}s.",
                    stream.Id, stream.DisconnectedAt, gracePeriodSeconds);
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("{Count} stream(s) auto-ended after grace period expired.", expiredStreams.Count);
        }
    }
}
