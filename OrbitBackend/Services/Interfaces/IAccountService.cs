using OrbitBackend.DTOs.Account;
using OrbitBackend.DTOs.Common;

namespace OrbitBackend.Services.Interfaces
{
    public interface IAccountService
    {
        Task<PaginatedResponseDto<AccountDto>> GetAccountsAsync(int page, int pageSize);
        Task DeleteAccountAsync(string userId);
    }
}
