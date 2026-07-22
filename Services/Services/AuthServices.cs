using Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Interfaces;

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
                return StatusCodes.Status401Unauthorized;
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogInformation("User: {userName} has not confirmed email.", user.UserName);
                return StatusCodes.Status401Unauthorized;
            }

            var roles = await _userManager.GetRolesAsync(user);

            if (roles == null || !roles.Any())
            {
                _logger.LogError("User: {userName}  has no roles assigned.", user.UserName);
                return StatusCodes.Status401Unauthorized;

            }

            var singIn = await _signInManager.PasswordSignInAsync(
                user.UserName,
                command.Password,
                isPersistent: false,
                lockoutOnFailure: false
                );

            if (!singIn.Succeeded)
            {
                _logger.LogError("User: {userName} failed to sign in.", user.UserName);
                return StatusCodes.Status401Unauthorized;
            }    

            return StatusCodes.Status204NoContent;
        }
    }
}
