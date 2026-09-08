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

        public AddUserCommand MapAdd(AddUserRequest request)
        {
            return new AddUserCommand
            {
                FirstName = NormalizeName(request.FirstName) ?? string.Empty,
                LastName = NormalizeName(request.LastName) ?? string.Empty,
                Email = request.Email.Trim().ToLowerInvariant(),
                Role = request.Role,
                Password = request.Password
            };
        }
    }
}
