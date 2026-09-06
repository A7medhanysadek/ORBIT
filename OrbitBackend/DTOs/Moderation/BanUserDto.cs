using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Moderation
{
    public class BanUserDto
    {
        /// <summary>
        /// Username of the user to ban from chat.
        /// </summary>
        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Optional reason for the ban.
        /// </summary>
        [MaxLength(500)]
        public string? Reason { get; set; }
    }
}
