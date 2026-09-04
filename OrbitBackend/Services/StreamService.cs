using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;
using System.Security.Cryptography;

namespace OrbitBackend.Services
{
    public class StreamService : IStreamService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ILogger<StreamService> _logger;
        private readonly IMediaServerConfigService _mediaServerConfig;

        public StreamService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IConfiguration config,
            ILogger<StreamService> logger,
            IMediaServerConfigService mediaServerConfig)
        {
            _context = context;
            _userManager = userManager;
            _config = config;
            _logger = logger;
            _mediaServerConfig = mediaServerConfig;
        }

        public async Task<StreamKeyResponseDto> GenerateStreamKeyAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            
            user.StreamKey = GenerateSecureStreamKey();
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Stream key generated for user {UserId}.", userId);

            return new StreamKeyResponseDto
            {
                StreamKey = user.StreamKey,
                RtmpUrl = GetRtmpIngestUrl(),
                Message = "Stream key generated successfully. Use this key in your streaming software (e.g., OBS)."
            };
        }

        public async Task<StreamKeyResponseDto> GetStreamKeyAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (string.IsNullOrEmpty(user.StreamKey))
                throw new InvalidOperationException("No stream key found. Please generate one first.");

            return new StreamKeyResponseDto
            {
                StreamKey = user.StreamKey,
                RtmpUrl = GetRtmpIngestUrl(),
                Message = "Use this stream key in your streaming software (e.g., OBS)."
            };
        }

        public async Task<StreamResponseDto> CreateStreamAsync(string userId, CreateStreamDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (string.IsNullOrEmpty(user.StreamKey))
                throw new InvalidOperationException("You must generate a stream key before creating a stream.");

            
            var existingLive = await _context.LiveStreams
                .AnyAsync(s => s.StreamerId == userId && s.IsLive);

            if (existingLive)
                throw new InvalidOperationException("You already have an active live stream. End it before creating a new one.");

            var stream = new LiveStream
            {
                Title = dto.Title,
                Description = dto.Description,
                IsLive = false,
                StreamerId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.LiveStreams.Add(stream);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream {StreamId} created by user {UserId}.", stream.Id, userId);

            return MapToResponseDto(stream, user);
        }

        public async Task<bool> ValidateStreamKeyAsync(string streamKey)
        {
            if (string.IsNullOrEmpty(streamKey))
                return false;

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StreamKey == streamKey);

            if (user == null)
            {
                _logger.LogWarning("Stream key validation failed — key not found.");
                return false;
            }

            
            var isStreamer = await _userManager.IsInRoleAsync(user, "Streamer");
            if (!isStreamer)
            {
                _logger.LogWarning("Stream key validation failed — user {UserId} is not a Streamer.", user.Id);
                return false;
            }

            
            var hasPendingStream = await _context.LiveStreams
                .AnyAsync(s => s.StreamerId == user.Id && !s.IsLive && s.EndedAt == null);

            if (!hasPendingStream)
            {
                _logger.LogWarning("Stream key validation failed — no pending stream for user {UserId}.", user.Id);
                return false;
            }

            _logger.LogInformation("Stream key validated for user {UserId}.", user.Id);
            return true;
        }

        public async Task MarkStreamLiveAsync(string streamKey)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StreamKey == streamKey)
                ?? throw new InvalidOperationException("Invalid stream key.");

            var stream = await _context.LiveStreams
                .Where(s => s.StreamerId == user.Id && !s.IsLive && s.EndedAt == null)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No pending stream found for this streamer.");

            stream.IsLive = true;
            stream.StartedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream {StreamId} is now LIVE for user {UserId}.", stream.Id, user.Id);
        }

        public async Task MarkStreamOfflineAsync(string streamKey)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StreamKey == streamKey);

            if (user == null)
            {
                _logger.LogWarning("on_publish_done received for unknown stream key.");
                return;
            }

            var stream = await _context.LiveStreams
                .Where(s => s.StreamerId == user.Id && s.IsLive)
                .FirstOrDefaultAsync();

            if (stream == null)
            {
                _logger.LogWarning("on_publish_done received but no live stream found for user {UserId}.", user.Id);
                return;
            }

            stream.IsLive = false;
            stream.EndedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream {StreamId} is now OFFLINE for user {UserId}.", stream.Id, user.Id);
        }

        public async Task EndStreamAsync(string userId)
        {
            var stream = await _context.LiveStreams
                .Where(s => s.StreamerId == userId && s.IsLive)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No active live stream found.");

            stream.IsLive = false;
            stream.EndedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Stream {StreamId} manually ended by user {UserId}.", stream.Id, userId);
        }

        public async Task<List<LiveStreamSummaryDto>> GetLiveStreamsAsync()
        {
            var hlsBaseUrl = _mediaServerConfig.GetHlsBaseUrl();

            return await _context.LiveStreams
                .Where(s => s.IsLive)
                .Include(s => s.Streamer)
                .Select(s => new LiveStreamSummaryDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    StreamerName = s.Streamer.FullName,
                    HlsUrl = $"{hlsBaseUrl}/{s.Streamer.StreamKey}.m3u8",
                    StartedAt = s.StartedAt
                })
                .ToListAsync();
        }

        public async Task<StreamResponseDto> GetStreamByIdAsync(int id)
        {
            var stream = await _context.LiveStreams
                .Include(s => s.Streamer)
                .FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new InvalidOperationException("Stream not found.");

            return MapToResponseDto(stream, stream.Streamer);
        }

        public async Task SaveRecordingPathAsync(string streamKey, string filePath)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StreamKey == streamKey);

            if (user == null)
            {
                _logger.LogWarning("on_record_done received for unknown stream key.");
                return;
            }

            // Find the most recently ended stream for this user
            var stream = await _context.LiveStreams
                .Where(s => s.StreamerId == user.Id && !s.IsLive && s.EndedAt != null)
                .OrderByDescending(s => s.EndedAt)
                .FirstOrDefaultAsync();

            if (stream == null)
            {
                _logger.LogWarning("on_record_done received but no ended stream found for user {UserId}.", user.Id);
                return;
            }

            // Extract just the file name from the full path
            stream.RecordingFileName = System.IO.Path.GetFileName(filePath);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Recording saved for stream {StreamId}: {FileName}",
                stream.Id, stream.RecordingFileName);
        }

        private static string GenerateSecureStreamKey()
        {
            
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        private string GetRtmpIngestUrl()
        {
            return _mediaServerConfig.GetRtmpUrl();
        }

        private StreamResponseDto MapToResponseDto(LiveStream stream, AppUser streamer)
        {
            var hlsBaseUrl = _mediaServerConfig.GetHlsBaseUrl();

            // Build VOD URL if the stream has a recording
            string? vodUrl = null;
            if (!stream.IsLive && !string.IsNullOrEmpty(stream.RecordingFileName))
            {
                var recordingsBaseUrl = _config["Streaming:RecordingsBaseUrl"]
                    ?? hlsBaseUrl.Replace("/hls", "/recordings");
                vodUrl = $"{recordingsBaseUrl}/{stream.RecordingFileName}";
            }

            return new StreamResponseDto
            {
                Id = stream.Id,
                Title = stream.Title,
                Description = stream.Description,
                IsLive = stream.IsLive,
                HlsUrl = stream.IsLive ? $"{hlsBaseUrl}/{streamer.StreamKey}.m3u8" : null,
                VodUrl = vodUrl,
                StreamerId = stream.StreamerId,
                StreamerName = streamer.FullName,
                StartedAt = stream.StartedAt,
                EndedAt = stream.EndedAt,
                CreatedAt = stream.CreatedAt
            };
        }
    }
}
