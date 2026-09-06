using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OrbitBackend.Models;

namespace OrbitBackend.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<LiveStream> LiveStreams { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Channel> Channels { get; set; }
        public DbSet<ChannelModerator> ChannelModerators { get; set; }
        public DbSet<ChatTimeout> ChatTimeouts { get; set; }
        public DbSet<ChatBan> ChatBans { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ── Channel ──
            builder.Entity<Channel>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ChannelName)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.HasIndex(e => e.ChannelName)
                    .IsUnique();

                entity.Property(e => e.Description)
                    .HasMaxLength(500);

                entity.HasIndex(e => e.StreamKey)
                    .IsUnique()
                    .HasFilter("[StreamKey] IS NOT NULL");

                // One-to-one: AppUser → Channel
                entity.HasOne(e => e.Owner)
                    .WithOne(u => u.Channel)
                    .HasForeignKey<Channel>(e => e.OwnerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.OwnerId)
                    .IsUnique();
            });

            // ── ChannelModerator (composite key) ──
            builder.Entity<ChannelModerator>(entity =>
            {
                entity.HasKey(e => new { e.ChannelId, e.UserId });

                entity.HasOne(e => e.Channel)
                    .WithMany(c => c.Moderators)
                    .HasForeignKey(e => e.ChannelId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.ModeratorOf)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.NoAction); // Avoid multiple cascade paths
            });

            // ── LiveStream ──
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
                    .OnDelete(DeleteBehavior.NoAction); // Avoid multiple cascade paths

                entity.HasOne(e => e.Channel)
                    .WithMany(c => c.LiveStreams)
                    .HasForeignKey(e => e.ChannelId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.IsLive);
                entity.HasIndex(e => e.StreamerId);
                entity.HasIndex(e => e.ChannelId);
            });

            // ── ChatMessage ──
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

            // ── ChatTimeout ──
            builder.Entity<ChatTimeout>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Reason)
                    .HasMaxLength(500);

                entity.HasOne(e => e.Channel)
                    .WithMany()
                    .HasForeignKey(e => e.ChannelId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Moderator)
                    .WithMany()
                    .HasForeignKey(e => e.ModeratorId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Index for checking if a user is currently timed out
                entity.HasIndex(e => new { e.ChannelId, e.UserId, e.ExpiresAt });
            });

            // ── ChatBan ──
            builder.Entity<ChatBan>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Reason)
                    .HasMaxLength(500);

                entity.HasOne(e => e.Channel)
                    .WithMany()
                    .HasForeignKey(e => e.ChannelId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Moderator)
                    .WithMany()
                    .HasForeignKey(e => e.ModeratorId)
                    .OnDelete(DeleteBehavior.NoAction);

                // Index for checking if a user is currently banned
                entity.HasIndex(e => new { e.ChannelId, e.UserId, e.IsActive });
            });
        }
    }
}
