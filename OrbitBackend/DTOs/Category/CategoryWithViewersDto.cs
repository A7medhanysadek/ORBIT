namespace OrbitBackend.DTOs.Category
{
    /// <summary>
    /// Category with aggregated live viewer count — used for the "Top Categories" homepage section.
    /// </summary>
    public class CategoryWithViewersDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Total number of viewers watching live streams in this category right now.
        /// </summary>
        public int TotalViewers { get; set; }

        /// <summary>
        /// Number of live streams currently in this category.
        /// </summary>
        public int LiveStreamCount { get; set; }
    }
}
