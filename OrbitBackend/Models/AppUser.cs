using Microsoft.AspNetCore.Identity;

namespace OrbitBackend.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        
        public string? StreamKey { get; set; }
        public ICollection<LiveStream> LiveStreams { get; set; } = new List<LiveStream>();
    }
}
