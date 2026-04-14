using Domain;
using Domain.Common;
using DTO.Request;
using DTO.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class AuthServices : IAuthServices
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly TokenServices _token;

        public AuthServices(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            TokenServices token
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _token = token;
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                u.Email == request.Name ||
                u.NormalizedUserName == request.Name.Trim().ToUpper()
            );

            if (user == null)
            {
                return Result<AuthResponse>.Failure(
                    "User not found.",
                    StatusCodes.Status404NotFound
                    );
            }

            if (!user.EmailConfirmed)
            {
                return Result<AuthResponse>.Failure(
                    "Email is not confirmed.",
                    StatusCodes.Status403Forbidden
                    );
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                request.Password,
                false,
                true
            );

            if (!result.Succeeded)
            {
                return Result<AuthResponse>.Failure(
                    "Invalid credentials.",
                    StatusCodes.Status401Unauthorized
                    );
            }

            var roles = await _userManager.GetRolesAsync(user);

            if (roles == null || !roles.Any())
            {
                return Result<AuthResponse>.Failure(
                    "User has no roles assigned.",
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
                        Email = user.Email,
                        UserName = user.UserName,
                        Roles = roles
                    }
                );
        }
    }
}
