using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OrbitBackend.DTOs.Auth;
using OrbitBackend.Models;
using OrbitBackend.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace OrbitBackend.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly IMapper _mapper;
        private readonly IMailService _mailService;

        private const string OtpProvider = "Orbit";
        private const string OtpPurpose = "EmailOTP";

        public AuthService(
            UserManager<AppUser> userManager,
            IConfiguration config,
            IMapper mapper,
            IMailService mailService)
        {
            _userManager = userManager;
            _config = config;
            _mapper = mapper;
            _mailService = mailService;
        }

        public async Task<RegisterResponseDto> RegisterAsync(RegisterDto dto)
        {
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);

            if (existingUser is not null)
            {
                if (existingUser.EmailConfirmed)
                    throw new InvalidOperationException("A user with this email already exists.");

                await _userManager.DeleteAsync(existingUser);
            }

            var user = _mapper.Map<AppUser>(dto);

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Registration failed: {errors}");
            }

            var otpCode = GenerateAlphanumericOtp();
            await _userManager.SetAuthenticationTokenAsync(user, OtpProvider, OtpPurpose, otpCode);

            await _mailService.SendConfirmationEmailAsync(user.Email!, user.FullName, otpCode);

            var response = _mapper.Map<RegisterResponseDto>(user);
            response.Message = "Registration successful. Please check your email to confirm your account.";
            return response;
        }

        public async Task<AuthResponseDto> ConfirmEmailAsync(ConfirmEmailDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email)
                ?? throw new InvalidOperationException("Invalid confirmation request.");

            if (user.EmailConfirmed)
                throw new InvalidOperationException("This email address has already been confirmed.");

            var storedOtp = await _userManager.GetAuthenticationTokenAsync(user, OtpProvider, OtpPurpose);

            if (string.IsNullOrEmpty(storedOtp) ||
                !string.Equals(storedOtp, dto.OTP_Code.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid or expired activation code.");
            }

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            await _userManager.RemoveAuthenticationTokenAsync(user, OtpProvider, OtpPurpose);

            return await BuildAuthResponseAsync(user);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user is not null)
            {
                if (!user.EmailConfirmed)
                    throw new InvalidOperationException("A user with this email does not confirmed his email.");
            }
            else
                throw new InvalidOperationException("Invalid email or password.");

            if (!await _userManager.CheckPasswordAsync(user, dto.Password))
                throw new InvalidOperationException("Invalid email or password.");

            return await BuildAuthResponseAsync(user);
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(string userId, RefreshTokenDto dto)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.RefreshToken != dto.RefreshToken || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new InvalidOperationException("Invalid or expired refresh token.");

            return await BuildAuthResponseAsync(user);
        }

        public async Task RevokeTokenAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _userManager.UpdateAsync(user);
        }

        public async Task ForgotPasswordAsync(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user is null) return;

            var otpCode = GenerateAlphanumericOtp();
            await _userManager.SetAuthenticationTokenAsync(user, OtpProvider, "PasswordResetOTP", otpCode);

            await _mailService.SendPasswordResetEmailAsync(user.Email!, user.FullName, otpCode);
        }

        public async Task ResetPasswordAsync(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email)
                ?? throw new InvalidOperationException("Invalid request.");

            var storedOtp = await _userManager.GetAuthenticationTokenAsync(user, OtpProvider, "PasswordResetOTP");

            if (string.IsNullOrEmpty(storedOtp) ||
                !string.Equals(storedOtp, dto.OTP_Code.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid or expired password reset code.");
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, dto.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Password reset failed: {errors}");
            }

            await _userManager.RemoveAuthenticationTokenAsync(user, OtpProvider, "PasswordResetOTP");

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _userManager.UpdateAsync(user);
        }

        private static string GenerateAlphanumericOtp(int length = 6)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            var result = new char[length];
            for (int i = 0; i < length; i++)
                result[i] = chars[bytes[i] % chars.Length];

            return new string(result);
        }

        private async Task<AuthResponseDto> BuildAuthResponseAsync(AppUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var (accessToken, accessTokenExpiry) = GenerateAccessToken(user, roles);
            var (refreshToken, refreshTokenExpiry) = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = refreshTokenExpiry;
            await _userManager.UpdateAsync(user);

            var response = _mapper.Map<AuthResponseDto>(user);
            response.AccessToken = accessToken;
            response.AccessTokenExpiry = accessTokenExpiry;
            response.RefreshToken = refreshToken;
            response.RefreshTokenExpiry = refreshTokenExpiry;

            return response;
        }

        private (string token, DateTime expiry) GenerateAccessToken(AppUser user, IList<string> roles)
        {
            var jwtSettings = _config.GetSection("JWT");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expirationMinutes = int.Parse(jwtSettings["AccessTokenExpirationMinutes"]!);
            var expiry = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new(JwtRegisteredClaimNames.Name, user.UserName!),
                new("fullName", user.FullName),
                new("age", user.Age.ToString()),
            };

            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: expiry,
                signingCredentials: creds
            );

            return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
        }

        private static (string token, DateTime expiry) GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return (Convert.ToBase64String(randomBytes), DateTime.UtcNow.AddDays(7));
        }
    }
}
