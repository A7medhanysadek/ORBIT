using AutoMapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrbitBackend.Data;
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
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IMapper _mapper;
        private readonly IMailService _mailService;

        private const string OtpProvider = "Orbit";
        private const string OtpPurpose = "EmailOTP";

        public AuthService(
            UserManager<AppUser> userManager,
            AppDbContext context,
            IConfiguration config,
            IMapper mapper,
            IMailService mailService)
        {
            _userManager = userManager;
            _context = context;
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

            // Mark as OG user if among the first 100 registered users
            var totalUsers = await _context.Users.CountAsync();
            if (totalUsers <= 100)
            {
                user.IsOgUser = true;
                await _userManager.UpdateAsync(user);
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

        public Task<AuthResponseDto> RefreshTokenAsync(string userId, RefreshTokenDto dto) => RefreshTokenAsync(dto, userId);

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto dto, string? userId = null)
        {
            if (string.IsNullOrWhiteSpace(dto.RefreshToken))
                throw new InvalidOperationException("Refresh token is required.");

            AppUser? user = null;
            if (!string.IsNullOrEmpty(userId))
            {
                user = await _userManager.FindByIdAsync(userId);
            }

            if (user == null)
            {
                user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);
            }

            if (user == null)
                throw new InvalidOperationException("Invalid refresh token.");

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

        public async Task<AuthResponseDto> GoogleLoginAsync(GoogleAuthDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Credential))
                throw new InvalidOperationException("Google credential is required.");

            // Verify Google Token via Google TokenInfo API
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(dto.Credential.Trim())}");
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("Google authentication failed or token is invalid/expired.");
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (!root.TryGetProperty("email", out var emailProp) || string.IsNullOrEmpty(emailProp.GetString()))
            {
                throw new InvalidOperationException("Google token did not contain a valid email.");
            }

            var email = emailProp.GetString()!;
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            var picture = root.TryGetProperty("picture", out var picProp) ? picProp.GetString() : null;

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Auto-register user from Google profile
                var username = email.Split('@')[0].Replace(".", "_").Replace("-", "_");
                var existingUserWithUsername = await _userManager.FindByNameAsync(username);
                if (existingUserWithUsername != null)
                {
                    username = $"{username}_{Guid.NewGuid().ToString("N")[..4]}";
                }

                user = new AppUser
                {
                    UserName = username,
                    Email = email,
                    FullName = string.IsNullOrWhiteSpace(name) ? username : name,
                    EmailConfirmed = true,
                    ProfilePictureUrl = picture,
                    Age = 18
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Could not create account: {errors}");
                }

                // Check for OG badge
                var totalUsers = await _context.Users.CountAsync();
                if (totalUsers <= 100)
                {
                    user.IsOgUser = true;
                    await _userManager.UpdateAsync(user);
                }
            }
            else
            {
                if (!user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                }
                if (string.IsNullOrEmpty(user.ProfilePictureUrl) && !string.IsNullOrEmpty(picture))
                {
                    user.ProfilePictureUrl = picture;
                }
                await _userManager.UpdateAsync(user);
            }

            return await BuildAuthResponseAsync(user);
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

            try
            {
                await _context.Entry(user).Reference(u => u.Channel).LoadAsync();
            }
            catch
            {
                // Ignore if already loaded or not tracking
            }

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
