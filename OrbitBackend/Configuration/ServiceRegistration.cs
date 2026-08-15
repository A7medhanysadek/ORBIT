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

            return services;
        }
    }
}
