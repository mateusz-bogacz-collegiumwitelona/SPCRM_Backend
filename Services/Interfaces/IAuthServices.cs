using Domain.Common;
using Services.Command.Auth;
using Services.Response.Auth;

namespace Services.Interfaces
{
    public interface IAuthServices
    {
        Task<int> LoginAsync(LoginCommand command);
        Task<int> LogoutAsync();
        Task<Result<AuthResponse>> GetUserDataAsync(Guid userId);
    }
}
