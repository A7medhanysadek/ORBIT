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
    }
}
