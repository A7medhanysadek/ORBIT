using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Channel
{
    public class HireModeratorDto
    {
        /// <summary>
        /// The username of the user to hire as a moderator.
        /// </summary>
        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Optional channel ID. If not specified, defaults to the authenticated user's owned channel.
        /// </summary>
        public int? ChannelId { get; set; }
    }
}
