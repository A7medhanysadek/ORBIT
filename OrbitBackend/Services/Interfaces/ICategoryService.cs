using OrbitBackend.DTOs.Category;
using OrbitBackend.DTOs.Streaming;

namespace OrbitBackend.Services.Interfaces
{
    public interface ICategoryService
    {
        Task<List<CategoryDto>> GetAllCategoriesAsync();
        Task<CategoryDto> GetCategoryBySlugAsync(string slug);
        Task<List<CategoryWithViewersDto>> GetTopCategoriesAsync(int count);
        Task<List<CategoryDto>> SearchCategoriesAsync(string query);
        Task<List<LiveStreamSummaryDto>> GetStreamsByCategoryAsync(string slug);
        Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto);
        Task<CategoryDto> UpdateCategoryAsync(int id, CreateCategoryDto dto);
        Task DeleteCategoryAsync(int id);
        Task<CategoryDto> UploadCategoryImageAsync(int id, IFormFile file);
    }
}
