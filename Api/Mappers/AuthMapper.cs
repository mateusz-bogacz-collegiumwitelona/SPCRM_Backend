using Api.Mappers.Helper;
using Api.Request.Auth;
using Riok.Mapperly.Abstractions;
using Services.Command.Auth;

namespace Api.Mappers
{
    [Mapper]
    public partial class AuthMapper
    {
        public LoginCommand MapLogin(LoginRequest request)
            => new LoginCommand
            {
                Name = request.Name,
                Password = request.Password
            };

        [MapProperty(nameof(ForgotPasswordRequest.Email), nameof(ForgotPasswordCommand.Email), Use = nameof(NormalizeEmail))]
        public partial ForgotPasswordCommand MapForgotPassword(ForgotPasswordRequest request);

        public ResetPasswordCommand MapResetPassword(ResetPasswordRequest request)
            => new ResetPasswordCommand
            {
                UserId = request.UserId,
                Token = request.Token,
                Password = request.Password
            };

        private string NormalizeEmail(string email) => StringNormalizerHelper.NormalizeEmail(email);
    }
}
