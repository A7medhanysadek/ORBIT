using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Category;
using OrbitBackend.DTOs.Clip;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Services.Interfaces;

namespace OrbitBackend.Controllers
{
    /// <summary>
    /// Manages stream categories. Public endpoints for browsing/searching,
    /// category clips, and admin endpoints for CRUD and image upload.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IClipService _clipService;

        public CategoryController(ICategoryService categoryService, IClipService clipService)
        {
            _categoryService = categoryService;
            _clipService = clipService;
        }

        /// <summary>
        /// Lists all available categories.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllCategories()
        {
            var result = await _categoryService.GetAllCategoriesAsync();
            return Ok(result);
        }

        /// <summary>
        /// Gets the top categories ranked by total live viewer count.
        /// Used for the homepage "Top Categories" section.
        /// </summary>
        [HttpGet("top")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<CategoryWithViewersDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopCategories([FromQuery] int count = 10)
        {
            var result = await _categoryService.GetTopCategoriesAsync(count);
            return Ok(result);
        }

        /// <summary>
        /// Searches categories by name.
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchCategories([FromQuery] string q = "")
        {
            var result = await _categoryService.SearchCategoriesAsync(q);
            return Ok(result);
        }

        /// <summary>
        /// Gets a single category by its URL-friendly slug.
        /// </summary>
        [HttpGet("{slug}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetCategoryBySlug(string slug)
        {
            var result = await _categoryService.GetCategoryBySlugAsync(slug);
            return Ok(result);
        }

        /// <summary>
        /// Gets all live streams in a category.
        /// </summary>
        [HttpGet("{slug}/streams")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<LiveStreamSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetStreamsByCategory(string slug)
        {
            var result = await _categoryService.GetStreamsByCategoryAsync(slug);
            return Ok(result);
        }

        /// <summary>
        /// Gets top watched clips in this category by category slug.
        /// </summary>
        [HttpGet("{slug}/clips")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ClipResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTopClipsByCategorySlug(string slug, [FromQuery] int count = 20)
        {
            var result = await _clipService.GetTopClipsByCategorySlugAsync(slug, count);
            return Ok(result);
        }

        /// <summary>
        /// Gets top watched clips in this category by category ID.
        /// </summary>
        [HttpGet("{id:int}/clips")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<ClipResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetTopClipsByCategoryId(int id, [FromQuery] int count = 20)
        {
            var result = await _clipService.GetTopClipsByCategoryAsync(id, count);
            return Ok(result);
        }

        /// <summary>
        /// Creates a new category. Admin only.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.CreateCategoryAsync(dto);
            return CreatedAtAction(nameof(GetCategoryBySlug), new { slug = result.Slug }, result);
        }

        /// <summary>
        /// Updates an existing category. Admin only.
        /// </summary>
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.UpdateCategoryAsync(id, dto);
            return Ok(result);
        }

        /// <summary>
        /// Deletes a category. Admin only.
        /// </summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            await _categoryService.DeleteCategoryAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Uploads a category box art image. Admin only.
        /// </summary>
        [HttpPost("{id:int}/image")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UploadCategoryImage(int id, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded." });

            var result = await _categoryService.UploadCategoryImageAsync(id, file);
            return Ok(result);
        }
    }
}
