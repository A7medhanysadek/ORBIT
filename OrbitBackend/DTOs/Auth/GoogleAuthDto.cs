using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Auth
{
    public class GoogleAuthDto
    {
        /// <summary>
        /// Google ID token or Google One-Tap credential string.
        /// </summary>
        [Required(ErrorMessage = "Google credential or ID token is required.")]
        public string Credential { get; set; } = string.Empty;
    }
}
