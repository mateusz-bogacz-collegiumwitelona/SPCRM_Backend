using Domain.Common;
using DTO.Request;
using Services.Response;

namespace Services.Interfaces
{
    public interface IAuthServices
    {
        Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
    }
}
