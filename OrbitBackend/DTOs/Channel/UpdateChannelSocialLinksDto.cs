using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Channel
{
    /// <summary>
    /// Replaces all social links for a channel with the provided list.
    /// </summary>
    public class UpdateChannelSocialLinksDto
    {
        [Required]
        public List<ChannelSocialLinkDto> SocialLinks { get; set; } = new();
    }
}
