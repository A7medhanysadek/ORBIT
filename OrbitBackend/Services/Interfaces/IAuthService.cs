using OrbitBackend.DTOs.Auth;

namespace OrbitBackend.Services.Interfaces
{
    public interface IAuthService
    {
        Task<RegisterResponseDto> RegisterAsync(RegisterDto dto);
        Task<AuthResponseDto> ConfirmEmailAsync(ConfirmEmailDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RefreshTokenAsync(string userId, RefreshTokenDto dto);
        Task RevokeTokenAsync(string userId);
        Task ForgotPasswordAsync(ForgotPasswordDto dto);
        Task ResetPasswordAsync(ResetPasswordDto dto);
    }
}
