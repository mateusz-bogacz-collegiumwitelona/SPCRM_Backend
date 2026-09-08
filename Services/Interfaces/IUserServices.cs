using Domain.Common;
using Services.Command.User;
using Services.Response.User;

namespace Services.Interfaces
{
    public interface IUserServices
    {
        Task<Result<List<UserSimpleListResponse>>> GetUserSimpleListAsync();
        Task<Result<PagedResult<UserListResponse>>> GetUserListAsync(UserListCommand command);
        Task<Result<List<OwnerResponse>>> GetAvailableOwnersAsync();
        Task<Result> CreateUserAsync(AddUserCommand command);
        Task<Result> ConfirmEmailAsync(ConfirmEmailCommand command);
        Task<Result> LockoutUserAsync(SetLockoutCommand command, Guid adminId);
        Task<Result> UnlockUserAsync(Guid userId, Guid adminId);
    }
}
