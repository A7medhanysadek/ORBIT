using Microsoft.EntityFrameworkCore;
using OrbitBackend.Data;
using OrbitBackend.Models;

namespace OrbitBackend.Data.Seeding
{
    public static class CategorySeeder
    {
        public static async Task SeedCategoriesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (await context.Categories.AnyAsync())
                return; // Already seeded

            var categories = new List<Category>
            {
                new() { Name = "Just Chatting", Slug = "just-chatting" },
                new() { Name = "Gaming", Slug = "gaming" },
                new() { Name = "Music", Slug = "music" },
                new() { Name = "Art", Slug = "art" },
                new() { Name = "IRL", Slug = "irl" },
                new() { Name = "Science & Technology", Slug = "science-technology" },
                new() { Name = "Sports", Slug = "sports" },
                new() { Name = "Education", Slug = "education" },
                new() { Name = "Esports", Slug = "esports" },
                new() { Name = "Talk Shows & Podcasts", Slug = "talk-shows-podcasts" },
                new() { Name = "Cooking", Slug = "cooking" },
                new() { Name = "Travel & Outdoors", Slug = "travel-outdoors" },
                new() { Name = "Fitness & Health", Slug = "fitness-health" },
                new() { Name = "Software Development", Slug = "software-development" },
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }
    }
}
