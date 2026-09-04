using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Models;

namespace OrbitBackend.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<LiveStream> LiveStreams { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

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

                entity.Property(e => e.RecordingFileName)
                    .HasMaxLength(500);

                entity.HasOne(e => e.Streamer)
                    .WithMany(u => u.LiveStreams)
                    .HasForeignKey(e => e.StreamerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.IsLive);
                entity.HasIndex(e => e.StreamerId);
            });

            builder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Content)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.SenderName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.SenderId)
                    .IsRequired();

                // FK → LiveStream
                entity.HasOne(e => e.LiveStream)
                    .WithMany(s => s.ChatMessages)
                    .HasForeignKey(e => e.LiveStreamId)
                    .OnDelete(DeleteBehavior.Cascade);

                // FK → AppUser
                entity.HasOne(e => e.Sender)
                    .WithMany(u => u.ChatMessages)
                    .HasForeignKey(e => e.SenderId)
                    .OnDelete(DeleteBehavior.NoAction); // Avoid multiple cascade paths

                // Index for loading chat by stream (most common query)
                entity.HasIndex(e => e.LiveStreamId);

                // Composite index for efficient VOD replay time-range queries
                entity.HasIndex(e => new { e.LiveStreamId, e.StreamOffsetSeconds });
            });
        }
    }
}
