using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface IAuthServices
    {
        Task<int> LoginAsync(LoginCommand command);
        Task<int> LogoutAsync();
        Task<Result<AuthResponse>> GetUserDataAsync(Guid userId);
    }
}
