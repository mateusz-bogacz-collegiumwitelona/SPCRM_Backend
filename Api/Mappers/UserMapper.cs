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
                Email = NormalizeEmail(request.Email),
                Token = request.Token,
                Password = request.Password
            };

        [MapProperty(nameof(AddUserRequest.FirstName), nameof(AddUserCommand.FirstName), Use = nameof(NormalizeRequiredName))]
        [MapProperty(nameof(AddUserRequest.LastName), nameof(AddUserCommand.LastName), Use = nameof(NormalizeRequiredName))]
        [MapProperty(nameof(AddUserRequest.Email), nameof(AddUserCommand.Email), Use = nameof(NormalizeEmail))]
        public partial AddUserCommand MapAdd(AddUserRequest request);

        public partial SetLockoutCommand MapSetLockout(SetLockoutRequest request);

        public partial DeleteUserCommand MapDelete(DeleteUserRequest request);

        [MapProperty(nameof(EditUserRequest.FirstName), nameof(EditUserCommand.FirstName), Use = nameof(NormalizeNullableName))]
        [MapProperty(nameof(EditUserRequest.LastName), nameof(EditUserCommand.LastName), Use = nameof(NormalizeNullableName))]
        public partial EditUserCommand MapEdit(EditUserRequest request);

        [MapProperty(nameof(ChangeUserEmailRequest.NewEmail), nameof(ChangeUserEmailCommand.NewEmail), Use = nameof(NormalizeEmail))]
        public partial ChangeUserEmailCommand MapChangeEmail(ChangeUserEmailRequest request);

        public partial ConfirmChangeUserEmailCommand MapConfirmChangeEmail(ConfirmChangeUserEmailRequest request);

        private string NormalizeRequiredName(string name) => StringNormalizerHelper.NormalizeName(name) ?? string.Empty;

        private string? NormalizeNullableName(string? name) => StringNormalizerHelper.NormalizeName(name);

        private string NormalizeEmail(string email) => StringNormalizerHelper.NormalizeEmail(email);
    }
}
