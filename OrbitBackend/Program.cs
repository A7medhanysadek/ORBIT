using Microsoft.EntityFrameworkCore;
using OrbitBackend.Configuration;
using OrbitBackend.Data;
using OrbitBackend.Data.Seeding;
using OrbitBackend.Hubs;
using OrbitBackend.Middleware;

namespace OrbitBackend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddIdentityConfiguration();
            builder.Services.AddJwtAuthentication(builder.Configuration);
            builder.Services.AddApplicationServices();
            builder.Services.AddControllers();
            builder.Services.AddSignalR();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSwaggerConfiguration();

            // CORS — required for SignalR WebSocket connections from browser clients
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials()
                          .SetIsOriginAllowed(_ => true); // Allow all origins in development
                });
            });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                await RoleSeeder.SeedRolesAsync(scope.ServiceProvider);
            }

            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseCors();

            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            // Map SignalR hub for real-time stream chat
            app.MapHub<StreamChatHub>("/hubs/stream-chat");

            app.Run();
        }
    }
}
