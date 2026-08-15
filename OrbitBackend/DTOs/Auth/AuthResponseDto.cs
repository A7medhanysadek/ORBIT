namespace OrbitBackend.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiry { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiry { get; set; }
    }
}
