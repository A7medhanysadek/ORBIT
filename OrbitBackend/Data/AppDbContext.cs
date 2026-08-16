using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Models;

namespace OrbitBackend.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<LiveStream> LiveStreams { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            
            builder.Entity<AppUser>()
                .HasIndex(u => u.StreamKey)
                .IsUnique()
                .HasFilter("[StreamKey] IS NOT NULL");

            
            builder.Entity<LiveStream>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Description)
                    .HasMaxLength(1000);

                entity.HasOne(e => e.Streamer)
                    .WithMany(u => u.LiveStreams)
                    .HasForeignKey(e => e.StreamerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.IsLive);
                entity.HasIndex(e => e.StreamerId);
            });
        }
    }
}
