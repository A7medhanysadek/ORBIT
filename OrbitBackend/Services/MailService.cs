using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Services
{
    public class MailService : IMailService
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public MailService(IConfiguration config, IWebHostEnvironment env)
        {
            _config = config;
            _env = env;
        }

        public async Task SendConfirmationEmailAsync(string toEmail, string toName, string otpCode)
        {
            var settings = ReadSettings();
            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "activation.ejs");
            var htmlTemplate = await File.ReadAllTextAsync(templatePath);

            var htmlBody = htmlTemplate
                .Replace("{0}", toName)
                .Replace("{1}", otpCode)
                .Replace("{2}", DateTime.UtcNow.Year.ToString())
                .Replace("{3}", settings.LogoUrl);

            await SendEmailAsync(
                settings, toEmail, toName,
                $"Your Orbit Activation Code: {otpCode}",
                htmlBody,
                $"Your Orbit activation code is: {otpCode}. It is valid for 30 minutes.");
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string otpCode)
        {
            var settings = ReadSettings();
            var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "reset-password.ejs");
            var htmlTemplate = await File.ReadAllTextAsync(templatePath);

            var htmlBody = htmlTemplate
                .Replace("{0}", toName)
                .Replace("{1}", otpCode)
                .Replace("{2}", DateTime.UtcNow.Year.ToString())
                .Replace("{3}", settings.LogoUrl);

            await SendEmailAsync(
                settings, toEmail, toName,
                $"Your Orbit Password Reset Code: {otpCode}",
                htmlBody,
                $"Your Orbit password reset code is: {otpCode}. It is valid for 30 minutes.");
        }

        private (string Host, int Port, bool EnableSsl, string SenderEmail, string SenderName,
                 string Password, string LogoUrl) ReadSettings()
        {
            var s = _config.GetSection("MailSettings");
            return (
                s["Host"]!,
                int.Parse(s["Port"]!),
                bool.Parse(s["EnableSsl"]!),
                s["SenderEmail"]!,
                s["SenderName"]!,
                s["Password"]!,
                s["LogoUrl"]!
            );
        }

        private static async Task SendEmailAsync(
            (string Host, int Port, bool EnableSsl, string SenderEmail, string SenderName,
             string Password, string LogoUrl) settings,
            string toEmail, string toName,
            string subject, string htmlBody, string textBody)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.SenderName, settings.SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

            try
            {
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.Auto);
                await smtp.AuthenticateAsync(settings.SenderEmail, settings.Password);
                await smtp.SendAsync(message);
                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to send email to {toEmail}. Reason: {ex.Message}", ex);
            }
        }
    }
}
