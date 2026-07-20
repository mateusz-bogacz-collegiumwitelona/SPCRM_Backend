using Api.Request;
using Riok.Mapperly.Abstractions;
using Services.Command;

namespace Api.Mappers
{
    [Mapper]
    public partial class AuthMapper
    {
        public partial LoginCommand MapLoginAsync(LoginRequest request);
    }
}
