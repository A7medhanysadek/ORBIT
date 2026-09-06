using OrbitBackend.Mapping;
using OrbitBackend.Services;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Configuration
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(MappingProfile));

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IMailService, MailService>();
            services.AddScoped<IStreamService, StreamService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IChannelService, ChannelService>();
            services.AddScoped<IModerationService, ModerationService>();
            services.AddSingleton<IMediaServerConfigService, MediaServerConfigService>();
            services.AddSingleton<ViewerTracker>();

            // Background service for auto-ending disconnected streams after grace period
            services.AddHostedService<StreamGracePeriodService>();

            return services;
        }
    }
}
