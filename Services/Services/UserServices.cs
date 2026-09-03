using Domain.Common;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
using Services.Response.User;

namespace Services.Services
{
    public class UserServices : IUserServices
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly ILogger<UserServices> _logger;

        public UserServices(
            UserManager<ApplicationUser> userManager, 
            RoleManager<IdentityRole<Guid>> roleManager,
            AppDbContext context,
            ILogger<UserServices> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<UserSimpleListResponse>>> GetUserSimpleListAsync()
        {
            var now = DateTimeOffset.UtcNow;

            var users = await (
                from user in _context.Users
                where user.EmailConfirmed
                   && (user.LockoutEnd == null || user.LockoutEnd <= now)
                   && !user.IsDeleted
                join userRole in _context.UserRoles on user.Id equals userRole.UserId
                join role in _context.Roles on userRole.RoleId equals role.Id
                where role.Name != "Admin"
                select new UserSimpleListResponse
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                }
            )
            .Distinct()
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync();

            return Result<List<UserSimpleListResponse>>.Success(
                message: "User list retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: users
            );
        }
    }
}
