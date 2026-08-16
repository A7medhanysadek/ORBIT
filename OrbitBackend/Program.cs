using Microsoft.EntityFrameworkCore;
using OrbitBackend.Configuration;
using OrbitBackend.Data;
using OrbitBackend.Data.Seeding;
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
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSwaggerConfiguration();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                await RoleSeeder.SeedRolesAsync(scope.ServiceProvider);
            }

            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI();

            
            

            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
