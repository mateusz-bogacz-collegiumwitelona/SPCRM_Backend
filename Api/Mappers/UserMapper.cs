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

        public ConfirmEmailCommand MapConfirmEmail(ConfirmEmailRequest request) 
            => new ConfirmEmailCommand
            {
                Email = NormalizeEmail(request.Email),
                Token = request.Token,
                Password = request.Password
            };

        public partial AddUserCommand MapAdd(AddUserRequest request);

        public partial SetLockoutCommand MapSetLockout(SetLockoutRequest request);
        
        public partial DeleteUserCommand MapDelete(DeleteUserRequest request);

        private string? NormalizeName(string? name) => StringNormalizerHelper.NormalizeName(name);

        private string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    }
}
