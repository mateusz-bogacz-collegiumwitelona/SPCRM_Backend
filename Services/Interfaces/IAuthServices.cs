using Domain.Common;
using DTO.Request;
using DTO.Response;

namespace Services.Interfaces
{
    public interface IAuthServices
    {
        Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
    }
}
