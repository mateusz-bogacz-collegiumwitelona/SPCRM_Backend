using Domain.Common;
using Domain.Comunication;
using Domain.Constants;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.User;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.User;

namespace Services.Services
{
    public class UserServices : IUserServices
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly ILogger<UserServices> _logger;
        private readonly IEmailSender _emailSender;

        public UserServices(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            AppDbContext context,
            ILogger<UserServices> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _logger = logger;
            _emailSender = emailSender;
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

        public async Task<Result<PagedResult<UserListResponse>>> GetUserListAsync(UserListCommand command)
        {
            var hasUsersWithoutRole = await _context.Users
                 .Where(u => !u.IsDeleted)
                 .AnyAsync(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id));

            if (hasUsersWithoutRole)
            {
                throw new MissingUserRoleException();
            }

            var now = DateTimeOffset.UtcNow;

            return await _context.Users
                .AsNoTracking()
                .ApplySearch(command.SearchTerm, _context)
                .ApplyFilter(command.Role, command.IsBlocked, _context)
                .ApplySorting(command.SortBy, command.SortDescending, _context)
                .Select(u => new UserListResponse
                {
                    Id = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = (from ur in _context.UserRoles
                            join r in _context.Roles on ur.RoleId equals r.Id
                            where ur.UserId == u.Id
                            select r.Name).FirstOrDefault() ?? string.Empty,
                    IsBlocked = u.LockoutEnd != null && u.LockoutEnd > now
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "users");
        }

        public async Task<Result<List<OwnerResponse>>> GetAvailableOwnersAsync()
        {
            var now = DateTimeOffset.UtcNow;

            var hasUsersWithoutRole = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted)
                .AnyAsync(u => !_context.UserRoles.Any(ur => ur.UserId == u.Id));

            if (hasUsersWithoutRole)
            {
                _logger.LogError("Data integrity violation: Found active users without an assigned role.");
                throw new MissingUserRoleException();
            }

            var owners = await (from u in _context.Users.AsNoTracking()
                                join ur in _context.UserRoles on u.Id equals ur.UserId
                                join r in _context.Roles on ur.RoleId equals r.Id
                                where !u.IsDeleted
                                      && (u.LockoutEnd == null || u.LockoutEnd <= now)
                                      && r.NormalizedName != "ADMIN"
                                orderby u.LastName, u.FirstName
                                select new OwnerResponse
                                {
                                    Id = u.Id,
                                    FirstName = u.FirstName,
                                    LastName = u.LastName,
                                    Role = r.Name!
                                })
                                .ToListAsync();

            return Result<List<OwnerResponse>>.Success(
                message: "Available owners retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: owners
            );
        }

        public async Task<Result> CreateUserAsync(AddUserCommand command)
        {
            bool isUserExist = await _context.Users
                 .AsNoTracking()
                 .AnyAsync(u => u.NormalizedEmail == command.Email.ToUpperInvariant());

            if (isUserExist) 
            { 
                _logger.LogWarning("Attempt to create a user with an existing email: {Email}", command.Email);

                return Result.Failure(
                    message: "A user with this email already exists.",
                    errorCode: ErrorCodes.UserAlreadyExists,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var firstPart = command.FirstName.Trim();
            var lastPart = command.LastName.Trim();

            var safeFirst = firstPart.Length >= 3 ? firstPart[..3] : firstPart;
            var safeLast = lastPart.Length >= 3 ? lastPart[..3] : lastPart;
            var baseUserName = $"{safeFirst}{safeLast}".ToLowerInvariant();

            string candidateUserName;
            var random = Random.Shared;
            do
            {
                candidateUserName = $"{baseUserName}{random.Next(100, 1000)}";
            }
            while (await _context.Users.AnyAsync(u => u.NormalizedUserName == candidateUserName.ToUpperInvariant()));

            var user = new ApplicationUser
            {
                FirstName = command.FirstName,
                LastName = command.LastName,
                Email = command.Email,
                UserName = candidateUserName,
                EmailConfirmed = false,
                LockoutEnabled = false
            };

            var createResult = await _userManager.CreateAsync(user, command.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Identity failed to create user {Email}: {Errors}", command.Email, errors);
                return Result.Failure(
                    message: $"Failed to create user: {errors}",
                    errorCode: ErrorCodes.BadRequest,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            IdentityResult roleResult;
            try
            {
                roleResult = await _userManager.AddToRoleAsync(user, command.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception thrown while assigning role {Role} to user {UserId}. Rolling back user creation.", command.Role, user.Id);
                await _userManager.DeleteAsync(user);
                throw new DataCorruptionException($"Failed to assign role to created user '{user.Id}'. Error: {ex.Message} || {ex.StackTrace} || {ex.InnerException?.Message}");
            }

            if (!roleResult.Succeeded)
            {
                _logger.LogError("Failed to assign role {Role} to newly created user {UserId}. Rolling back user creation.",
                    command.Role, user.Id);
                await _userManager.DeleteAsync(user);
                throw new DataCorruptionException($"Failed to assign role to created user '{user.Id}'.");
            }

            var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            if (string.IsNullOrEmpty(confirmationToken))
            {
                _logger.LogError("Critical Identity error: Generated empty confirmation token for user {UserId}.", user.Id);
                await _userManager.DeleteAsync(user);
                throw new DataCorruptionException($"Failed to generate email confirmation token for user '{user.Id}'.");
            }

            var createDomain = new CreateUserDomain
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                UserName = user.UserName,
                Token = confirmationToken
            };

            await _emailSender.SendCreateUserEmailAsync(createDomain);
            
            _logger.LogInformation("User {UserId} ('{UserName}', '{Email}') created successfully with role {Role}.",
                user.Id, user.UserName, user.Email, command.Role);
            
            return Result.Success(
                message: "User created successfully.",
                statusCode: StatusCodes.Status201Created
            );
        }
    }
}
