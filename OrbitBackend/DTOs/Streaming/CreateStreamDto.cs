using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Streaming
{
    public class CreateStreamDto
    {
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }
    }
}
