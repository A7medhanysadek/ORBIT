namespace OrbitBackend.DTOs.Auth
{
    public class RegisterResponseDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Message { get; set; } = "Registration successful. Please log in.";
    }
}
