using Api.Request.Auth;
using Riok.Mapperly.Abstractions;
using Services.Command.Auth;

namespace Api.Mappers
{
    [Mapper]
    public partial class AuthMapper
    {
        public partial LoginCommand MapLoginAsync(LoginRequest request);
    }
}
