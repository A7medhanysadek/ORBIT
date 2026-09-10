using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Streaming
{
    /// <summary>
    /// Request DTO for updating stream metadata (title, description, category) while live or pending.
    /// </summary>
    public class UpdateLiveStreamDto
    {
        [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
        public string? Title { get; set; }

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string? Description { get; set; }

        /// <summary>
        /// Optional category ID to switch to while streaming (e.g., changing games).
        /// </summary>
        public int? CategoryId { get; set; }
    }
}
