using Domain.Common;
using Services.Command.Auth;
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
        Task<Result> DeleteUserAsync(DeleteUserCommand command, Guid currentUserId);
        Task<Result> EditUserAsync(EditUserCommand command, Guid currentUserId);
        Task<Result> ChangeUserEmailAsync(ChangeUserEmailCommand command, Guid adminId);
        Task<Result> ConfirmChangeUserEmailAsync(ConfirmChangeUserEmailCommand command);
        Task<Result> ForgotPasswordAsync(ForgotPasswordCommand command);
        Task<Result> ResetPasswordAsync(ResetPasswordCommand command);
        Task<Result> ChangeRoleAsync(ChangeRoleCommand command, Guid adminId);
        Task<Result<UserDetailResponse>> GetUserDetailAsync(Guid userId);
        Task<Result<List<string>>> GetRolesAsync();
    }
}
