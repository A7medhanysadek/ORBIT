using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Channel
{
    /// <summary>
    /// DTO for updating channel profile information (description, donation, save streams).
    /// </summary>
    public class UpdateChannelProfileDto
    {
        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(500)]
        [Url(ErrorMessage = "Donation URL must be a valid URL.")]
        public string? DonationUrl { get; set; }

        [MaxLength(500)]
        public string? DonationMessage { get; set; }

        public bool? SaveStreams { get; set; }
    }
}
