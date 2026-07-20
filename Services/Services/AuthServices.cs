using Domain.Common;
using Domain.Constants;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Interfaces;
using Services.Response;

namespace Services.Services
{
    public class AuthServices : IAuthServices
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TokenServices _token;
        private readonly ILogger<AuthServices> _logger;

        public AuthServices(
            UserManager<ApplicationUser> userManager,
            TokenServices token,
            ILogger<AuthServices> logger
            )
        {
            _userManager = userManager;
            _token = token;
            _logger = logger;
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginCommand command)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                u.Email == command.Name ||
                u.NormalizedUserName == command.Name.Trim().ToUpper()
            );


            bool isPasswordValidate = false;


            if (user != null)
            {
                isPasswordValidate = await _userManager.CheckPasswordAsync(user, command.Password);
            }

            if (user == null || !isPasswordValidate)
            {
                _logger.LogInformation("Invalid username or password.");
                return Result<AuthResponse>.Failure(
                    "Invalid username or password.",
                    ErrorCodes.InvalidCredentials,
                    StatusCodes.Status401Unauthorized
                    );
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogInformation("User: {userName} has not confirmed email.", user.UserName);
                return Result<AuthResponse>.Failure(
                    "Email is not confirmed.",
                    ErrorCodes.EmailNotConfirmed,
                    StatusCodes.Status403Forbidden
                    );
            }

            var roles = await _userManager.GetRolesAsync(user);

            if (roles == null || !roles.Any())
            {
                _logger.LogError("User: {userName}  has no roles assigned.", user.UserName);
                return Result<AuthResponse>.Failure(
                    "User has no roles assigned.",
                    ErrorCodes.NoRolesAssigned,
                    StatusCodes.Status403Forbidden
                    );
            }

            string token = _token.CreateJwtToken(user, roles);

            return Result<AuthResponse>.Success(
                "Login successful.",
                StatusCodes.Status200OK,
                new AuthResponse
                {
                    Token = token,
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    UserName = user.UserName ?? "",
                    Roles = roles
                }
            );
        }
    }
}
