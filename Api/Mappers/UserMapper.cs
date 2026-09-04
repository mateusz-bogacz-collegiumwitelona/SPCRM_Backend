using Api.Mappers.Helper;
using Api.Request.User;
using Riok.Mapperly.Abstractions;
using Services.Command.User;

namespace Api.Mappers
{
    [Mapper]
    public partial class UserMapper
    {
        [MapProperty(nameof(UserListRequest.Role), nameof(UserListRequest.Role), Use = nameof(NormalizeName))]
        public partial UserListCommand MapList(UserListRequest request);

        private string? NormalizeName(string? name) => StringNormalizerHelper.NormalizeName(name);
    }
}
