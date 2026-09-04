using Domain.Common;
using Services.Command.User;
using Services.Response.User;

namespace Services.Interfaces
{
    public interface IUserServices
    {
        Task<Result<List<UserSimpleListResponse>>> GetUserSimpleListAsync();
        Task<Result<PagedResult<UserListResponse>>> GetUserListAsync(UserListCommand command);
    }
}
