using System.ComponentModel.DataAnnotations;

namespace OrbitBackend.DTOs.Category
{
    public class CreateCategoryDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [RegularExpression(@"^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Slug must be lowercase with hyphens only (e.g., 'just-chatting').")]
        public string Slug { get; set; } = string.Empty;
    }
}
