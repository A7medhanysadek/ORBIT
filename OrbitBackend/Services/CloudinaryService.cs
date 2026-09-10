using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryService> _logger;

        public CloudinaryService(IConfiguration configuration, ILogger<CloudinaryService> logger)
        {
            _logger = logger;

            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<string> UploadImageAsync(IFormFile file, string folder)
        {
            if (file.Length == 0)
                throw new InvalidOperationException("File is empty.");

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                throw new InvalidOperationException("Only JPEG, PNG, WebP, and GIF images are allowed.");

            // Max 5MB
            if (file.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("Image size must not exceed 5MB.");

            using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
            {
                _logger.LogError("Cloudinary upload error: {Error}", result.Error.Message);
                throw new InvalidOperationException($"Image upload failed: {result.Error.Message}");
            }

            _logger.LogInformation("Image uploaded to Cloudinary: {PublicId}", result.PublicId);
            return result.SecureUrl.ToString();
        }

        public async Task<string> UploadVideoAsync(IFormFile file, string folder)
        {
            if (file.Length == 0)
                throw new InvalidOperationException("File is empty.");

            // Max 100MB for video clips
            if (file.Length > 100 * 1024 * 1024)
                throw new InvalidOperationException("Video size must not exceed 100MB.");

            using var stream = file.OpenReadStream();

            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.Error != null)
            {
                _logger.LogError("Cloudinary video upload error: {Error}", result.Error.Message);
                throw new InvalidOperationException($"Video upload failed: {result.Error.Message}");
            }

            _logger.LogInformation("Video uploaded to Cloudinary: {PublicId}", result.PublicId);
            return result.SecureUrl.ToString();
        }

        public async Task DeleteVideoAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId)
            {
                ResourceType = ResourceType.Video
            };
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Result == "ok")
            {
                _logger.LogInformation("Video deleted from Cloudinary: {PublicId}", publicId);
            }
            else
            {
                _logger.LogWarning("Cloudinary video delete returned: {Result} for {PublicId}", result.Result, publicId);
            }
        }

        public async Task DeleteImageAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Result == "ok")
            {
                _logger.LogInformation("Image deleted from Cloudinary: {PublicId}", publicId);
            }
            else
            {
                _logger.LogWarning("Cloudinary delete returned: {Result} for {PublicId}", result.Result, publicId);
            }
        }

        public string GetPublicIdFromUrl(string url)
        {
            // Cloudinary URLs look like:
            // https://res.cloudinary.com/{cloud}/image/upload/v123456/folder/filename.ext
            // Public ID is: folder/filename (without extension)
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;

                // Find the part after "/upload/" (or "/upload/v{version}/")
                var uploadIndex = path.IndexOf("/upload/", StringComparison.Ordinal);
                if (uploadIndex < 0) return string.Empty;

                var afterUpload = path[(uploadIndex + "/upload/".Length)..];

                // Skip version segment if present (e.g., "v1234567890/")
                if (afterUpload.StartsWith("v") && afterUpload.Contains('/'))
                {
                    var versionEnd = afterUpload.IndexOf('/');
                    afterUpload = afterUpload[(versionEnd + 1)..];
                }

                // Remove file extension
                var lastDot = afterUpload.LastIndexOf('.');
                if (lastDot > 0)
                    afterUpload = afterUpload[..lastDot];

                return afterUpload;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
