using Api.Mappers.Helper;
using Api.Request.User;
using Riok.Mapperly.Abstractions;
using Services.Command.User;

namespace Api.Mappers
{
    [Mapper]
    public partial class UserMapper
    {
        [MapProperty(nameof(UserListRequest.Role), nameof(UserListRequest.Role), Use = nameof(NormalizeNullableName))]
        public partial UserListCommand MapList(UserListRequest request);

        public ConfirmEmailCommand MapConfirmEmail(ConfirmEmailRequest request)
            => new ConfirmEmailCommand
            {
                Email = NormalizeRequiredEmail(request.Email),
                Token = request.Token,
                Password = request.Password
            };

        [MapProperty(nameof(AddUserRequest.FirstName), nameof(AddUserCommand.FirstName), Use = nameof(NormalizeRequiredName))]
        [MapProperty(nameof(AddUserRequest.LastName), nameof(AddUserCommand.LastName), Use = nameof(NormalizeRequiredName))]
        [MapProperty(nameof(AddUserRequest.Email), nameof(AddUserCommand.Email), Use = nameof(NormalizeRequiredEmail))]
        public partial AddUserCommand MapAdd(AddUserRequest request);

        public partial SetLockoutCommand MapSetLockout(SetLockoutRequest request);

        public partial DeleteUserCommand MapDelete(DeleteUserRequest request);

        [MapProperty(nameof(EditUserRequest.FirstName), nameof(EditUserCommand.FirstName), Use = nameof(NormalizeNullableName))]
        [MapProperty(nameof(EditUserRequest.LastName), nameof(EditUserCommand.LastName), Use = nameof(NormalizeNullableName))]
        [MapProperty(nameof(EditUserRequest.Email), nameof(EditUserCommand.Email), Use = nameof(NormalizeNullableEmail))]
        public partial EditUserCommand MapEdit(EditUserRequest request);

        private string NormalizeRequiredName(string name) => StringNormalizerHelper.NormalizeName(name) ?? string.Empty;

        private string? NormalizeNullableName(string? name) => StringNormalizerHelper.NormalizeName(name);

        private string NormalizeRequiredEmail(string email) => email.Trim().ToLowerInvariant();

        private string? NormalizeNullableEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
    }
}
