using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.DTOs.Admin;
using OrbitBackend.DTOs.Dashboard;

namespace OrbitBackend.Tests
{
    public class CreatorStudioDashboardTests
    {
        [Fact]
        public void DashboardLifetimeStatsDto_ShouldTrackFiveCoreBroadcastMetrics()
        {
            // Arrange & Act
            var stats = new DashboardLifetimeStatsDto
            {
                TotalBroadcastHours = "12h 30m",
                AllTimePeakViewers = 450,
                TotalVodViews = 1200,
                TotalClipViews = 3500,
                UniqueChattersCount = 310
            };

            // Assert: Verify the 5 lifetime broadcast metrics tracked by Creator Studio
            Assert.Equal("12h 30m", stats.TotalBroadcastHours);
            Assert.Equal(450, stats.AllTimePeakViewers);
            Assert.Equal(1200, stats.TotalVodViews);
            Assert.Equal(3500, stats.TotalClipViews);
            Assert.Equal(310, stats.UniqueChattersCount);
        }

        [Fact]
        public void StreamerDashboardSummaryDto_InitializesDefaultLifetimeStats()
        {
            // Arrange & Act
            var summary = new StreamerDashboardSummaryDto
            {
                ChannelId = 1,
                ChannelName = "OrbitGamer"
            };

            // Assert
            Assert.NotNull(summary.LifetimeStats);
            Assert.Equal(0, summary.LifetimeStats.AllTimePeakViewers);
            Assert.Equal("0h", summary.LifetimeStats.TotalBroadcastHours);
        }
    }

    public class PlatformAdminDashboardTests
    {
        [Fact]
        public void AdminStatsDto_ShouldMonitorSevenCoreSystemEntities()
        {
            // Arrange & Act
            var stats = new AdminStatsDto
            {
                TotalUsers = 1500,
                TotalChannels = 320,
                ActiveStreams = 12,
                TotalClips = 840,
                TotalVods = 610,
                TotalCategories = 25,
                TotalChatMessages = 45000
            };

            // Assert: Verify the 7 core system entities monitored by Platform Admin
            Assert.Equal(1500, stats.TotalUsers);
            Assert.Equal(320, stats.TotalChannels);
            Assert.Equal(12, stats.ActiveStreams);
            Assert.Equal(840, stats.TotalClips);
            Assert.Equal(610, stats.TotalVods);
            Assert.Equal(25, stats.TotalCategories);
            Assert.Equal(45000, stats.TotalChatMessages);
        }

        [Fact]
        public void AdminStatsDto_EntityCountPropertyCheck()
        {
            // Verify exactly 7 metric properties represent the 7 core system entities
            var properties = typeof(AdminStatsDto).GetProperties();
            Assert.Equal(7, properties.Length);

            var propertyNames = properties.Select(p => p.Name).ToList();
            Assert.Contains(nameof(AdminStatsDto.TotalUsers), propertyNames);
            Assert.Contains(nameof(AdminStatsDto.TotalChannels), propertyNames);
            Assert.Contains(nameof(AdminStatsDto.ActiveStreams), propertyNames);
            Assert.Contains(nameof(AdminStatsDto.TotalClips), propertyNames);
            Assert.Contains(nameof(AdminStatsDto.TotalVods), propertyNames);
            Assert.Contains(nameof(AdminStatsDto.TotalCategories), propertyNames);
            Assert.Contains(nameof(AdminStatsDto.TotalChatMessages), propertyNames);
        }
    }
}

