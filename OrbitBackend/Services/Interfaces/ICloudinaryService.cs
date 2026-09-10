namespace OrbitBackend.Services.Interfaces
{
    /// <summary>
    /// Cloudinary image upload/delete abstraction.
    /// </summary>
    public interface ICloudinaryService
    {
        /// <summary>
        /// Uploads an image to Cloudinary and returns the secure URL.
        /// </summary>
        /// <param name="file">The uploaded file.</param>
        /// <param name="folder">Cloudinary folder (e.g., "orbit/profile-pictures").</param>
        /// <returns>The secure URL of the uploaded image.</returns>
        Task<string> UploadImageAsync(IFormFile file, string folder);

        /// <summary>
        /// Deletes an image from Cloudinary by its public ID.
        /// </summary>
        /// <param name="publicId">The Cloudinary public ID of the image.</param>
        Task DeleteImageAsync(string publicId);

        /// <summary>
        /// Uploads a video to Cloudinary and returns the secure URL.
        /// </summary>
        Task<string> UploadVideoAsync(IFormFile file, string folder);

        /// <summary>
        /// Deletes a video from Cloudinary by its public ID.
        /// </summary>
        Task DeleteVideoAsync(string publicId);

        /// <summary>
        /// Extracts the Cloudinary public ID from a full URL.
        /// </summary>
        string GetPublicIdFromUrl(string url);
    }
}
