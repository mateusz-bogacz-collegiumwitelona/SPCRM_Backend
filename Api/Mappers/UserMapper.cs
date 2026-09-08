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

        [MapProperty(nameof(ConfirmEmailRequest.Email), nameof(ConfirmEmailRequest.Email), Use = nameof(NormalizeEmail))]
        public partial ConfirmEmailCommand MapConfirmEmail(ConfirmEmailRequest request);

        public AddUserCommand MapAdd(AddUserRequest request)
        {
            return new AddUserCommand
            {
                FirstName = NormalizeName(request.FirstName) ?? string.Empty,
                LastName = NormalizeName(request.LastName) ?? string.Empty,
                Email = NormalizeEmail(request.Email),
                Role = request.Role,
                Password = request.Password
            };
        }

        public partial SetLockoutCommand MapSetLockout(SetLockoutRequest request);

        private string? NormalizeName(string? name) => StringNormalizerHelper.NormalizeName(name);

        private string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
