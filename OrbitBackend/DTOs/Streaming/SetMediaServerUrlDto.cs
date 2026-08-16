using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Streaming
{
    public class SetMediaServerUrlDto
    {
        
        
        
        
        [Required]
        public string RtmpUrl { get; set; } = string.Empty;

        
        
        
        
        [Required]
        public string HlsBaseUrl { get; set; } = string.Empty;
    }
}
