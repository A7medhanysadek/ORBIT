using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Auth
{
    public class ConfirmEmailDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string OTP_Code { get; set; } = string.Empty;
    }
}
