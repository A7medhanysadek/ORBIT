using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.Hubs;
using OrbitBackend.Services.Interfaces;
using System.Text.RegularExpressions;

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

                // Auto-assign and merge recording files if channel has SaveStreams enabled
                if (stream.Channel != null && stream.Channel.SaveStreams)
                {
                    try
                    {
                        var streamService = scope.ServiceProvider.GetRequiredService<IStreamService>();
                        await streamService.FinalizeStreamRecordingAsync(stream, stream.Channel);
                    }
                    catch (Exception finalizeEx)
                    {
                        _logger.LogWarning(finalizeEx, "Failed to finalize recording for stream {StreamId}", stream.Id);
                        var startedUtc = DateTime.SpecifyKind(stream.StartedAt ?? stream.CreatedAt, DateTimeKind.Utc);
                        var endedUtc = DateTime.SpecifyKind(stream.EndedAt ?? DateTime.UtcNow, DateTimeKind.Utc);
                        long startEpoch = new DateTimeOffset(startedUtc).ToUnixTimeSeconds();
                        long endEpoch = new DateTimeOffset(endedUtc).ToUnixTimeSeconds();

                        var resolvedFile = TryFindLatestRecordingOnDisk(stream.Channel.StreamKey, startEpoch, endEpoch);
                        if (!string.IsNullOrEmpty(resolvedFile))
                        {
                            stream.RecordingFileName = resolvedFile;
                        }
                    }
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
                    "Disconnected at {DisconnectedAt}, grace period {GracePeriod}s, peak viewers {Peak}, recording: {Recording}.",
                    stream.Id, stream.DisconnectedAt, gracePeriodSeconds, stream.PeakViewers, stream.RecordingFileName ?? "None");
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("{Count} stream(s) auto-ended after grace period expired.", expiredStreams.Count);
        }

        private string? TryFindLatestRecordingOnDisk(string? streamKey, long? startEpoch = null, long? endEpoch = null)
        {
            if (string.IsNullOrEmpty(streamKey)) return null;

            try
            {
                var candidateDirs = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "StreamingServer", "nginx", "recordings"),
                    Path.Combine(Directory.GetCurrentDirectory(), "StreamingServer", "nginx", "recordings"),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "StreamingServer", "nginx", "recordings")
                };

                foreach (var dir in candidateDirs)
                {
                    if (Directory.Exists(dir))
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        var query = dirInfo.GetFiles($"{streamKey}*.flv")
                            .Concat(dirInfo.GetFiles($"{streamKey}*.mp4"))
                            .Where(f => f.Length > 0 && !f.Name.Contains("-merged"));

                        if (startEpoch.HasValue && endEpoch.HasValue)
                        {
                            var pattern = "^" + Regex.Escape(streamKey) + @"-(\d{9,12})";
                            var patternDate = "^" + Regex.Escape(streamKey) + @".*?(\d{4})(\d{2})(\d{2})-(\d{2})(\d{2})(\d{2})";
                            query = query.Where(f =>
                            {
                                long ep;
                                var m = Regex.Match(f.Name, pattern);
                                var mDate = Regex.Match(f.Name, patternDate);
                                if (m.Success && long.TryParse(m.Groups[1].Value, out long parsed))
                                {
                                    ep = parsed;
                                }
                                else if (mDate.Success && int.TryParse(mDate.Groups[1].Value, out int y))
                                {
                                    int mo = int.Parse(mDate.Groups[2].Value);
                                    int d = int.Parse(mDate.Groups[3].Value);
                                    int h = int.Parse(mDate.Groups[4].Value);
                                    int min = int.Parse(mDate.Groups[5].Value);
                                    int s = int.Parse(mDate.Groups[6].Value);
                                    ep = new DateTimeOffset(new DateTime(y, mo, d, h, min, s, DateTimeKind.Utc)).ToUnixTimeSeconds();
                                }
                                else
                                {
                                    ep = new DateTimeOffset(f.LastWriteTimeUtc).ToUnixTimeSeconds();
                                }
                                return ep >= (startEpoch.Value - 300) && ep <= (endEpoch.Value + 300);
                            });
                        }

                        var latestFile = query.OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
                        if (latestFile != null)
                        {
                            _logger.LogInformation("Found latest recording on disk for {StreamKey}: {FileName}", streamKey, latestFile.Name);
                            return latestFile.Name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error scanning recordings directory for stream key {StreamKey}", streamKey);
            }

            return null;
        }
    }
}
