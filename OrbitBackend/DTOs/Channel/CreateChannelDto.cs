using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Channel
{
    public class CreateChannelDto
    {
        /// <summary>
        /// Unique display name for the channel.
        /// </summary>
        [Required]
        [MaxLength(50)]
        [MinLength(3)]
        public string ChannelName { get; set; } = string.Empty;

        /// <summary>
        /// Optional description/bio for the channel.
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }
    }
}
