using Domain.Common;
using DTO.Request;
using DTO.Response;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IAuthServices
    {
        Task<Result<AuthResponse>> LoginAsync(LoginRequest request);
    }
}
