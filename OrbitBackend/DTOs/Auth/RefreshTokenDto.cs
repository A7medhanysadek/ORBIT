using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Auth
{
    public class RefreshTokenDto
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
