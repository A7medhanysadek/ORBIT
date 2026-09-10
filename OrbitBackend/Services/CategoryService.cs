using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Category;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IMediaServerConfigService _mediaServerConfig;
        private readonly ViewerTracker _viewerTracker;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(
            AppDbContext context,
            ICloudinaryService cloudinaryService,
            IMediaServerConfigService mediaServerConfig,
            ViewerTracker viewerTracker,
            ILogger<CategoryService> logger)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
            _mediaServerConfig = mediaServerConfig;
            _viewerTracker = viewerTracker;
            _logger = logger;
        }

        public async Task<List<CategoryDto>> GetAllCategoriesAsync()
        {
            return await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    ImageUrl = c.ImageUrl
                })
                .ToListAsync();
        }

        public async Task<CategoryDto> GetCategoryBySlugAsync(string slug)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Slug == slug)
                ?? throw new InvalidOperationException("Category not found.");

            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                ImageUrl = category.ImageUrl
            };
        }

        public async Task<List<CategoryWithViewersDto>> GetTopCategoriesAsync(int count)
        {
            if (count < 1) count = 10;
            if (count > 50) count = 50;

            // Get all categories that have at least one live stream
            var categoriesWithStreams = await _context.Categories
                .AsNoTracking()
                .Include(c => c.LiveStreams)
                .Where(c => c.LiveStreams.Any(s => s.IsLive))
                .ToListAsync();

            // Calculate viewer counts in memory using the ViewerTracker
            var result = categoriesWithStreams
                .Select(c =>
                {
                    var liveStreams = c.LiveStreams.Where(s => s.IsLive).ToList();
                    return new CategoryWithViewersDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        Slug = c.Slug,
                        ImageUrl = c.ImageUrl,
                        LiveStreamCount = liveStreams.Count,
                        TotalViewers = liveStreams.Sum(s => _viewerTracker.GetViewerCount(s.Id))
                    };
                })
                .OrderByDescending(c => c.TotalViewers)
                .Take(count)
                .ToList();

            return result;
        }

        public async Task<List<CategoryDto>> SearchCategoriesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<CategoryDto>();

            return await _context.Categories
                .AsNoTracking()
                .Where(c => c.Name.Contains(query))
                .OrderBy(c => c.Name)
                .Take(20)
                .Select(c => new CategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    Slug = c.Slug,
                    ImageUrl = c.ImageUrl
                })
                .ToListAsync();
        }

        public async Task<List<LiveStreamSummaryDto>> GetStreamsByCategoryAsync(string slug)
        {
            var hlsBaseUrl = _mediaServerConfig.GetHlsBaseUrl();

            var streams = await _context.LiveStreams
                .AsNoTracking()
                .Where(s => s.IsLive && s.Category != null && s.Category.Slug == slug)
                .Include(s => s.Streamer)
                .Include(s => s.Channel)
                .Include(s => s.Category)
                .ToListAsync();

            return streams.Select(s => new LiveStreamSummaryDto
            {
                Id = s.Id,
                Title = s.Title,
                Description = s.Description,
                StreamerName = s.Streamer.FullName,
                ChannelName = s.Channel.ChannelName,
                ChannelId = s.ChannelId,
                HlsUrl = $"{hlsBaseUrl}/{s.Channel.StreamKey}.m3u8",
                ThumbnailUrl = s.ThumbnailUrl,
                StartedAt = s.StartedAt,
                CategoryId = s.CategoryId,
                CategoryName = s.Category?.Name,
                CategorySlug = s.Category?.Slug,
                IsReconnecting = s.DisconnectedAt != null,
                ViewerCount = _viewerTracker.GetViewerCount(s.Id)
            }).ToList();
        }

        public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto)
        {
            // Check uniqueness
            var exists = await _context.Categories.AnyAsync(c => c.Slug == dto.Slug);
            if (exists)
                throw new InvalidOperationException($"A category with slug '{dto.Slug}' already exists.");

            var category = new Category
            {
                Name = dto.Name,
                Slug = dto.Slug,
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category '{Name}' (slug: {Slug}) created.", category.Name, category.Slug);

            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                ImageUrl = category.ImageUrl
            };
        }

        public async Task<CategoryDto> UpdateCategoryAsync(int id, CreateCategoryDto dto)
        {
            var category = await _context.Categories.FindAsync(id)
                ?? throw new InvalidOperationException("Category not found.");

            // Check slug uniqueness if changed
            if (category.Slug != dto.Slug)
            {
                var slugTaken = await _context.Categories.AnyAsync(c => c.Slug == dto.Slug && c.Id != id);
                if (slugTaken)
                    throw new InvalidOperationException($"A category with slug '{dto.Slug}' already exists.");
            }

            category.Name = dto.Name;
            category.Slug = dto.Slug;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {Id} updated to '{Name}' (slug: {Slug}).", id, dto.Name, dto.Slug);

            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                ImageUrl = category.ImageUrl
            };
        }

        public async Task DeleteCategoryAsync(int id)
        {
            var category = await _context.Categories.FindAsync(id)
                ?? throw new InvalidOperationException("Category not found.");

            // Delete image from Cloudinary if exists
            if (!string.IsNullOrEmpty(category.ImageUrl))
            {
                var publicId = _cloudinaryService.GetPublicIdFromUrl(category.ImageUrl);
                if (!string.IsNullOrEmpty(publicId))
                    await _cloudinaryService.DeleteImageAsync(publicId);
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {Id} ('{Name}') deleted.", id, category.Name);
        }

        public async Task<CategoryDto> UploadCategoryImageAsync(int id, IFormFile file)
        {
            var category = await _context.Categories.FindAsync(id)
                ?? throw new InvalidOperationException("Category not found.");

            // Delete old image if exists
            if (!string.IsNullOrEmpty(category.ImageUrl))
            {
                var oldPublicId = _cloudinaryService.GetPublicIdFromUrl(category.ImageUrl);
                if (!string.IsNullOrEmpty(oldPublicId))
                    await _cloudinaryService.DeleteImageAsync(oldPublicId);
            }

            var url = await _cloudinaryService.UploadImageAsync(file, "orbit/categories");
            category.ImageUrl = url;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Category {Id} image updated.", id);

            return new CategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                ImageUrl = category.ImageUrl
            };
        }
    }
}
