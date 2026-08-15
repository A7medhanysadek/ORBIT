namespace OrbitBackend.Services.Interfaces
{
    public interface IMailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken);
        Task SendConfirmationEmailAsync(string toEmail, string toName, string otpCode);
    }
}
