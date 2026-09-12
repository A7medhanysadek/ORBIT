using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Clip;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class ClipService : IClipService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IMediaServerConfigService _mediaServerConfig;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ClipService> _logger;

        public ClipService(
            AppDbContext context,
            UserManager<AppUser> userManager,
            ICloudinaryService cloudinaryService,
            IMediaServerConfigService mediaServerConfig,
            IHttpClientFactory httpClientFactory,
            ILogger<ClipService> logger)
        {
            _context = context;
            _userManager = userManager;
            _cloudinaryService = cloudinaryService;
            _mediaServerConfig = mediaServerConfig;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        public async Task<ClipResponseDto> CreateClipAsync(string userId, CreateClipDto dto, IFormFile? videoFile)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            var channel = await _context.Channels
                .FirstOrDefaultAsync(c => c.Id == dto.ChannelId)
                ?? throw new InvalidOperationException("Channel not found.");

            string videoUrl;
            if (videoFile != null && videoFile.Length > 0)
            {
                videoUrl = await _cloudinaryService.UploadVideoAsync(videoFile, "orbit/clips");
            }
            else if (!string.IsNullOrWhiteSpace(dto.VideoUrl))
            {
                videoUrl = dto.VideoUrl.Trim();
            }
            else
            {
                throw new InvalidOperationException("Either a video file or VideoUrl must be provided.");
            }

            int? categoryId = dto.CategoryId;
            string? streamTitle = null;
            int? resolvedLiveStreamId = dto.LiveStreamId;

            if (dto.LiveStreamId.HasValue)
            {
                var stream = await _context.LiveStreams
                    .FirstOrDefaultAsync(s => s.Id == dto.LiveStreamId.Value)
                    ?? throw new InvalidOperationException("Specified live stream not found.");

                streamTitle = stream.Title;
                // Inherit category from stream if not explicitly set
                categoryId ??= stream.CategoryId;
            }
            else
            {
                // Auto-resolve: associate clip with the channel's current live stream if any
                var currentLiveStream = await _context.LiveStreams
                    .Where(s => s.ChannelId == channel.Id && s.IsLive)
                    .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                    .FirstOrDefaultAsync();

                if (currentLiveStream != null)
                {
                    resolvedLiveStreamId = currentLiveStream.Id;
                    streamTitle = currentLiveStream.Title;
                    categoryId ??= currentLiveStream.CategoryId;
                }
            }

            if (categoryId.HasValue)
            {
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == categoryId.Value);
                if (!categoryExists)
                    throw new InvalidOperationException("Specified category does not exist.");
            }

            var clip = new Clip
            {
                Title = dto.Title.Trim(),
                VideoUrl = videoUrl,
                ThumbnailUrl = dto.ThumbnailUrl,
                DurationSeconds = dto.DurationSeconds,
                ViewCount = 0,
                CreatedAt = DateTime.UtcNow,
                CreatorId = userId,
                ChannelId = channel.Id,
                LiveStreamId = resolvedLiveStreamId,
                CategoryId = categoryId
            };

            _context.Clips.Add(clip);
            await _context.SaveChangesAsync();

            // Load related navigations for response
            await _context.Entry(clip).Reference(c => c.Creator).LoadAsync();
            await _context.Entry(clip).Reference(c => c.Channel).LoadAsync();
            if (clip.CategoryId.HasValue)
            {
                await _context.Entry(clip).Reference(c => c.Category).LoadAsync();
            }
            if (clip.LiveStreamId.HasValue)
            {
                await _context.Entry(clip).Reference(c => c.LiveStream).LoadAsync();
            }

            _logger.LogInformation("Clip {ClipId} '{Title}' created by user {UserId} on channel {ChannelId}.", clip.Id, clip.Title, userId, channel.Id);

            return MapToResponseDto(clip);
        }

        public async Task<List<ClipResponseDto>> GetChannelClipsAsync(int channelId, int page = 1, int pageSize = 20)
        {
            var channelExists = await _context.Channels.AnyAsync(c => c.Id == channelId);
            if (!channelExists)
                throw new InvalidOperationException("Channel not found.");

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var clips = await _context.Clips
                .AsNoTracking()
                .Where(c => c.ChannelId == channelId)
                .Include(c => c.Creator)
                .Include(c => c.Channel)
                .Include(c => c.Category)
                .Include(c => c.LiveStream)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return clips.Select(MapToResponseDto).ToList();
        }

        public async Task<List<ClipResponseDto>> GetTopClipsAsync(int count = 20)
        {
            count = Math.Clamp(count, 1, 100);

            var clips = await _context.Clips
                .AsNoTracking()
                .Include(c => c.Creator)
                .Include(c => c.Channel)
                .Include(c => c.Category)
                .Include(c => c.LiveStream)
                .OrderByDescending(c => c.ViewCount)
                .ThenByDescending(c => c.CreatedAt)
                .Take(count)
                .ToListAsync();

            return clips.Select(MapToResponseDto).ToList();
        }

        public async Task<List<ClipResponseDto>> GetTopClipsByCategoryAsync(int categoryId, int count = 20)
        {
            var categoryExists = await _context.Categories.AnyAsync(c => c.Id == categoryId);
            if (!categoryExists)
                throw new InvalidOperationException("Category not found.");

            count = Math.Clamp(count, 1, 100);

            var clips = await _context.Clips
                .AsNoTracking()
                .Where(c => c.CategoryId == categoryId)
                .Include(c => c.Creator)
                .Include(c => c.Channel)
                .Include(c => c.Category)
                .Include(c => c.LiveStream)
                .OrderByDescending(c => c.ViewCount)
                .ThenByDescending(c => c.CreatedAt)
                .Take(count)
                .ToListAsync();

            return clips.Select(MapToResponseDto).ToList();
        }

        public async Task<List<ClipResponseDto>> GetTopClipsByCategorySlugAsync(string slug, int count = 20)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == slug.ToLower())
                ?? throw new InvalidOperationException($"Category '{slug}' not found.");

            return await GetTopClipsByCategoryAsync(category.Id, count);
        }

        public async Task<ClipResponseDto> GetClipByIdAsync(int clipId)
        {
            var clip = await _context.Clips
                .AsNoTracking()
                .Include(c => c.Creator)
                .Include(c => c.Channel)
                .Include(c => c.Category)
                .Include(c => c.LiveStream)
                .FirstOrDefaultAsync(c => c.Id == clipId)
                ?? throw new InvalidOperationException("Clip not found.");

            return MapToResponseDto(clip);
        }

        public async Task RecordClipViewAsync(int clipId)
        {
            var clip = await _context.Clips.FirstOrDefaultAsync(c => c.Id == clipId)
                ?? throw new InvalidOperationException("Clip not found.");

            clip.ViewCount++;
            await _context.SaveChangesAsync();
        }

        public async Task DeleteClipAsync(int clipId, string userId)
        {
            var clip = await _context.Clips
                .Include(c => c.Channel)
                .FirstOrDefaultAsync(c => c.Id == clipId)
                ?? throw new InvalidOperationException("Clip not found.");

            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isCreator = clip.CreatorId == userId;
            var isChannelOwner = clip.Channel.OwnerId == userId;

            if (!isAdmin && !isCreator && !isChannelOwner)
            {
                throw new UnauthorizedAccessException("You do not have permission to delete this clip.");
            }

            // If video is hosted on Cloudinary, attempt deletion
            var publicId = _cloudinaryService.GetPublicIdFromUrl(clip.VideoUrl);
            if (!string.IsNullOrEmpty(publicId))
            {
                try
                {
                    await _cloudinaryService.DeleteVideoAsync(publicId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete clip video from Cloudinary: {PublicId}", publicId);
                }
            }

            _context.Clips.Remove(clip);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Clip {ClipId} deleted by user {UserId}.", clipId, userId);
        }

        public async Task<ClipResponseDto> SliceLiveClipAsync(string userId, SliceClipDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            // Resolve stream and channel
            LiveStream? stream = null;
            Channel? channel = null;

            if (dto.LiveStreamId.HasValue)
            {
                stream = await _context.LiveStreams
                    .Include(s => s.Channel)
                    .Include(s => s.Category)
                    .FirstOrDefaultAsync(s => s.Id == dto.LiveStreamId.Value)
                    ?? throw new InvalidOperationException("Stream not found.");
                channel = stream.Channel;
            }
            else if (dto.ChannelId.HasValue)
            {
                channel = await _context.Channels
                    .FirstOrDefaultAsync(c => c.Id == dto.ChannelId.Value)
                    ?? throw new InvalidOperationException("Channel not found.");

                // Prioritize active live stream, then fall back to most recent stream
                stream = await _context.LiveStreams
                    .Include(s => s.Category)
                    .Where(s => s.ChannelId == channel.Id && s.IsLive)
                    .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                    .FirstOrDefaultAsync();

                // Fall back to most recent stream if no live stream found
                stream ??= await _context.LiveStreams
                    .Include(s => s.Category)
                    .Where(s => s.ChannelId == channel.Id)
                    .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
                    .FirstOrDefaultAsync();
            }
            else
            {
                throw new InvalidOperationException("Either LiveStreamId or ChannelId must be provided.");
            }

            if (channel == null || string.IsNullOrEmpty(channel.StreamKey))
                throw new InvalidOperationException("Channel or stream key not found.");

            // Clamp duration between 5 and 300 seconds (default 60)
            int durationSeconds = dto.DurationSeconds ?? 60;
            if (durationSeconds < 5) durationSeconds = 5;
            if (durationSeconds > 300) durationSeconds = 300;

            // Call media server clipping endpoint via HTTP (Cloud-Ready, decoupled HTTP call)
            var clipApiUrl = _mediaServerConfig.GetClipServiceUrl();
            bool isLive = stream != null && stream.IsLive;
            var payload = new
            {
                streamKey = channel.StreamKey,
                recordingFileName = isLive ? null : stream?.RecordingFileName,
                isLive = isLive,
                durationSeconds = durationSeconds,
                title = dto.Title.Trim()
            };

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync(clipApiUrl, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to media server clipping endpoint at {Url}", clipApiUrl);
                throw new InvalidOperationException($"Media server clipping service is unreachable at {clipApiUrl}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Media server clipping service returned {StatusCode}: {Error}", response.StatusCode, errContent);
                throw new InvalidOperationException($"Media server failed to create clip: {errContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<MediaServerClipResponse>();
            if (result == null || !result.Success || string.IsNullOrEmpty(result.ClipUrl))
            {
                throw new InvalidOperationException(result?.Error ?? "Failed to slice clip on media server.");
            }

            // Build full public URLs
            var clipsBaseUrl = _mediaServerConfig.GetClipsBaseUrl();
            var videoUrl = $"{clipsBaseUrl}/{result.ClipFileName}";
            var thumbnailUrl = !string.IsNullOrEmpty(result.ThumbnailUrl)
                ? $"{clipsBaseUrl}/{System.IO.Path.GetFileName(result.ThumbnailUrl)}"
                : stream?.ThumbnailUrl;

            int? categoryId = stream?.CategoryId;

            var clip = new Clip
            {
                Title = dto.Title.Trim(),
                VideoUrl = videoUrl,
                ThumbnailUrl = thumbnailUrl,
                DurationSeconds = durationSeconds,
                ViewCount = 0,
                CreatedAt = DateTime.UtcNow,
                CreatorId = userId,
                ChannelId = channel.Id,
                LiveStreamId = stream?.Id,
                CategoryId = categoryId
            };

            _context.Clips.Add(clip);
            await _context.SaveChangesAsync();

            await _context.Entry(clip).Reference(c => c.Creator).LoadAsync();
            await _context.Entry(clip).Reference(c => c.Channel).LoadAsync();
            if (clip.CategoryId.HasValue)
                await _context.Entry(clip).Reference(c => c.Category).LoadAsync();
            if (clip.LiveStreamId.HasValue)
                await _context.Entry(clip).Reference(c => c.LiveStream).LoadAsync();

            _logger.LogInformation("Clip {ClipId} sliced successfully from stream {StreamId} by user {UserId}.",
                clip.Id, stream?.Id, userId);

            return MapToResponseDto(clip);
        }

        private static ClipResponseDto MapToResponseDto(Clip clip)
        {
            return new ClipResponseDto
            {
                Id = clip.Id,
                Title = clip.Title,
                VideoUrl = clip.VideoUrl,
                ThumbnailUrl = clip.ThumbnailUrl,
                DurationSeconds = clip.DurationSeconds,
                ViewCount = clip.ViewCount,
                CreatedAt = clip.CreatedAt,
                CreatorId = clip.CreatorId,
                CreatorName = clip.Creator?.FullName ?? string.Empty,
                CreatorProfilePictureUrl = clip.Creator?.ProfilePictureUrl,
                ChannelId = clip.ChannelId,
                ChannelName = clip.Channel?.ChannelName ?? string.Empty,
                ChannelProfilePhotoUrl = clip.Channel?.ProfilePhotoUrl,
                LiveStreamId = clip.LiveStreamId,
                StreamTitle = clip.LiveStream?.Title,
                CategoryId = clip.CategoryId,
                CategoryName = clip.Category?.Name,
                CategorySlug = clip.Category?.Slug,
                CategoryImageUrl = clip.Category?.ImageUrl
            };
        }
    }

    internal class MediaServerClipResponse
    {
        public bool Success { get; set; }
        public string? ClipFileName { get; set; }
        public string? ClipUrl { get; set; }
        public string? ThumbnailUrl { get; set; }
        public int DurationSeconds { get; set; }
        public string? Error { get; set; }
    }
}
