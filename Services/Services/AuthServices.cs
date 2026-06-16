using Domain.Common;
using Domain.Constants;
using Domain.Models;
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
        private readonly TokenServices _token;

        public AuthServices(
            UserManager<ApplicationUser> userManager,
            TokenServices token
            )
        {
            _userManager = userManager;
            _token = token;
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
        {
            try
            {


                var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                    u.Email == request.Name ||
                    u.NormalizedUserName == request.Name.Trim().ToUpper()
                );


                bool isPasswordValidate = false;


                if (user != null)
                {
                    isPasswordValidate = await _userManager.CheckPasswordAsync(user, request.Password);
                }

                if (user == null || !isPasswordValidate)
                {
                    return Result<AuthResponse>.Failure(
                        "Invalid username or password.",
                        ErrorCodes.InvalidCredentials,
                        StatusCodes.Status401Unauthorized
                        );
                }

                if (!user.EmailConfirmed)
                {
                    return Result<AuthResponse>.Failure(
                        "Email is not confirmed.",
                        ErrorCodes.EmailNotConfirmed,
                        StatusCodes.Status403Forbidden
                        );
                }

                var roles = await _userManager.GetRolesAsync(user);

                if (roles == null || !roles.Any())
                {
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
            catch (Exception)
            {
                return Result<AuthResponse>.Failure(
                    "An error occurred during login.",
                    ErrorCodes.InternalError,
                    StatusCodes.Status500InternalServerError
                );
            }
        }
    }
}
