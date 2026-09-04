using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Chat
{
    public class SendChatMessageDto
    {
        [Required]
        public int StreamId { get; set; }

        [Required]
        [StringLength(500, MinimumLength = 1, ErrorMessage = "Message must be between 1 and 500 characters.")]
        public string Content { get; set; } = string.Empty;
    }
}
