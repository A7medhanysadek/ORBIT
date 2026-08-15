using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbitBackend.DTOs.Account;
using OrbitBackend.DTOs.Common;
using OrbitBackend.Services.Interfaces;
using System.Security.Claims;

namespace OrbitBackend.Controllers
{
    [ApiController]
    [Route("api/accounts-management")]
    [Authorize(Roles = "Admin")]
    [Produces("application/json")]
    public class AccountsManagementController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountsManagementController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet("accounts")]
        [ProducesResponseType(typeof(PaginatedResponseDto<AccountDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAccounts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _accountService.GetAccountsAsync(page, pageSize);
            return Ok(result);
        }

        [HttpDelete("accounts/{userId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteAccount(string userId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? User.FindFirstValue("sub");

            if (string.Equals(currentUserId, userId, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "You cannot delete your own account." });

            await _accountService.DeleteAccountAsync(userId);
            return NoContent();
        }
    }
}
