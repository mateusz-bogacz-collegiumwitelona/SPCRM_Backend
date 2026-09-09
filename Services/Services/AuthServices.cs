using Domain.Common;
using Domain.Constants;
using Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Auth;
using Services.Interfaces;
using Services.Response.Auth;

namespace Services.Services
{
    public class AuthServices : IAuthServices
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TokenServices _token;
        private readonly ILogger<AuthServices> _logger;
        private readonly SignInManager<ApplicationUser> _signInManager;
        public AuthServices(
            UserManager<ApplicationUser> userManager,
            TokenServices token,
            ILogger<AuthServices> logger,
            SignInManager<ApplicationUser> signInManager
            )
        {
            _userManager = userManager;
            _token = token;
            _logger = logger;
            _signInManager = signInManager;
        }

        public async Task<int> LoginAsync(LoginCommand command)
        {
            var normalizedInput = command.Name.Trim().ToUpperInvariant();

            var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                !u.IsDeleted &&
                (u.NormalizedEmail == normalizedInput || u.NormalizedUserName == normalizedInput)
            );

            if (user == null)
            {
                _logger.LogInformation("Invalid username or password.");
                return StatusCodes.Status401Unauthorized;
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogInformation("User: {UserName} has not confirmed email.", user.UserName);
                return StatusCodes.Status401Unauthorized;
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (roles == null || !roles.Any())
            {
                _logger.LogError("User: {UserName} has no roles assigned.", user.UserName);
                return StatusCodes.Status401Unauthorized;
            }

            var userName = user.UserName ?? string.Empty;

            var signInResult = await _signInManager.PasswordSignInAsync(
                userName,
                command.Password,
                isPersistent: false,
                lockoutOnFailure: false
            );

            if (signInResult.IsLockedOut)
            {
                _logger.LogWarning("User: {UserName} account is locked out.", user.UserName);
                return StatusCodes.Status423Locked;
            }

            if (!signInResult.Succeeded)
            {
                _logger.LogWarning("User: {UserName} failed to sign in.", user.UserName);
                return StatusCodes.Status401Unauthorized;
            }

            return StatusCodes.Status204NoContent;
        }

        public async Task<int> LogoutAsync()
        {
            await _signInManager.SignOutAsync();
            return StatusCodes.Status204NoContent;
        }

        public async Task<Result<AuthResponse>> GetUserDataAsync(Guid userId)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogInformation("User with ID: {userId} not found.", userId);
                return Result<AuthResponse>.Failure(
                    message: "User not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                    );
            }

            var roles = await _userManager.GetRolesAsync(user);

            if (roles == null || !roles.Any())
            {
                _logger.LogInformation("User with ID: {userId} has no roles assigned.", userId);
                return Result<AuthResponse>.Failure(
                    message: "User has no roles assigned",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NoRolesAssigned
                    );
            }

            AuthResponse response = new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Roles = roles.ToList()
            };

            return Result<AuthResponse>.Success(
                message: "User data retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }
    }
}
