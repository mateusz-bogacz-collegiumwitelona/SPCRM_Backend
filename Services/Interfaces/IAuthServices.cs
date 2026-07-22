using Domain.Common;
using Services.Command;
using Services.Response;

namespace Services.Interfaces
{
    public interface IAuthServices
    {
        Task<int> LoginAsync(LoginCommand command);
    }
}
