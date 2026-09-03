using Domain.Common;
using Services.Response.User;
using System;
using System.Collections.Generic;
using System.Text;

namespace Services.Interfaces
{
    public interface IUserServices
    {
        Task<Result<List<UserSimpleListResponse>>> GetUserSimpleListAsync();
    }
}
