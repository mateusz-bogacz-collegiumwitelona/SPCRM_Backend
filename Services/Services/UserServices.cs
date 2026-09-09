using Domain.Common;
using Domain.Comunication;
using Domain.Constants;
using Domain.Enum;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Auth;
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
                .Where(u => !u.IsDeleted)
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

            var createResult = await _userManager.CreateAsync(user);
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
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));

                _logger.LogError("Failed to assign role {Role} to newly created user {UserId}. Rolling back user creation. Errors: {errors} ",
                    command.Role, user.Id, errors);
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

        public async Task<Result> ConfirmEmailAsync(ConfirmEmailCommand command)
        {
            var user = await _userManager.FindByEmailAsync(command.Email);

            if (user == null)
            {
                _logger.LogWarning("User with email {Email} not found.", command.Email);
                return Result.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (user.EmailConfirmed)
            {
                _logger.LogInformation("Email already confirmed for user: {Email}", command.Email);
                return Result.Failure(
                    message: "Email is already confirmed.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            var confirmResult = await _userManager.ConfirmEmailAsync(user, command.Token);
            if (!confirmResult.Succeeded)
            {
                var errors = string.Join(", ", confirmResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Email confirmation failed for {Email}. Errors: {Errors}", command.Email, errors);

                return Result.Failure(
                    message: "Invalid or expired confirmation token.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.TokenInvalid
                );
            }

            var passwordResult = await _userManager.AddPasswordAsync(user, command.Password);
            if (!passwordResult.Succeeded)
            {
                user.EmailConfirmed = false;
                await _userManager.UpdateAsync(user);

                var passwordErrors = string.Join(", ", passwordResult.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to set password for user {Email}. Errors: {Errors}", command.Email, passwordErrors);

                return Result.Failure(
                    message: "Failed to set password. Please ensure it meets the required criteria.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.PasswordSetFailed
                );
            }

            _logger.LogInformation("Email successfully confirmed and password set for user: {Email}", command.Email);

            return Result.Success(
                message: "Email confirmed successfully. You can now log in.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> LockoutUserAsync(SetLockoutCommand command, Guid adminId)
        {
            var user = await _userManager.FindByIdAsync(command.UserId.ToString());

            if (user == null)
            {
                _logger.LogWarning("User with id {UserId} not found.", command.UserId);
                return Result.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                _logger.LogWarning("Cannot ban an admin: {Email}", user.NormalizedEmail);
                return Result.Failure(
                    message: "Cannot ban an admin.",
                    statusCode: StatusCodes.Status403Forbidden,
                    errorCode: ErrorCodes.CannotBlockAdmin
                );
            }

            await _userManager.SetLockoutEnabledAsync(user, true);

            DateTimeOffset lockoutEnd = command.LockoutEnd.HasValue
                ? DateTime.SpecifyKind(command.LockoutEnd.Value, DateTimeKind.Utc)
                : DateTimeOffset.MaxValue;

            await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await _emailSender.SendLockoutEmailAsync(user.Email, lockoutEnd);
            }

            _logger.LogInformation("User {Email} has been locked out by admin with id {AdminId}. LockoutEnd: {LockoutEnd}",
                user.NormalizedEmail, adminId, lockoutEnd);

            return Result.Success(
                message: "User locked out successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> UnlockUserAsync(Guid userId, Guid adminId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userId);
                return Result.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var isLockedOut = await _userManager.IsLockedOutAsync(user);
            if (!isLockedOut)
            {
                _logger.LogWarning("User {Email} is not currently locked out, cannot unlock.", user.Email);
                return Result.Failure(
                    message: "User is not locked out.",
                    errorCode: ErrorCodes.UserNotLockedOut,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            await _userManager.SetLockoutEndDateAsync(user, null);

            await _userManager.ResetAccessFailedCountAsync(user);

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await _emailSender.SendUnlockEmailAsync(user.Email);
            }

            _logger.LogInformation("User {Email} ({UserId}) has been unlocked by admin with ID {AdminId}.",
                user.Email, user.Id, adminId);

            return Result.Success(
                message: "User unlocked successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> DeleteUserAsync(DeleteUserCommand command, Guid currentUserId)
        {
            if (command.UserId == currentUserId)
            {
                _logger.LogWarning("User {UserId} attempted to delete themselves.", currentUserId);
                return Result.Failure(
                    message: "You cannot delete your own account.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var userToDelete = await _userManager.FindByIdAsync(command.UserId.ToString());
            if (userToDelete == null || userToDelete.IsDeleted)
            {
                _logger.LogWarning("User with ID {UserId} not found or already deleted.", command.UserId);
                return Result.Failure(
                    message: "User not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            if (await _userManager.IsInRoleAsync(userToDelete, "Admin"))
            {
                _logger.LogWarning("Attempted to delete admin account: {UserId}.", command.UserId);
                return Result.Failure(
                    message: "Cannot delete an administrator account.",
                    statusCode: StatusCodes.Status403Forbidden,
                    errorCode: ErrorCodes.CannotBlockAdmin
                );
            }

            var targetUser = await _userManager.FindByIdAsync(command.ReassignToUserId.ToString());

            if (targetUser == null || targetUser.IsDeleted)
            {
                _logger.LogWarning("Target user for reassignment {TargetUserId} not found or inactive.", command.ReassignToUserId);
                return Result.Failure(
                    message: "Target user for reassignment does not exist or is inactive.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.UserNotFound
                );
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var companies = await _context.Companies
                    .Where(c => c.OwnerId == command.UserId)
                    .ToListAsync();

                foreach (var company in companies)
                {
                    company.OwnerId = command.ReassignToUserId;
                }

                var contacts = await _context.Contacts
                    .Where(c => c.OwnerId == command.UserId)
                    .ToListAsync();

                foreach (var contact in contacts)
                {
                    contact.OwnerId = command.ReassignToUserId;
                }

                var activeDeals = await _context.Deals
                    .Where(d => d.OwnerId == command.UserId &&
                               (d.Status == DealsStatusEnum.ToDo || d.Status == DealsStatusEnum.InProgress))
                    .ToListAsync();

                foreach (var deal in activeDeals)
                {
                    deal.OwnerId = command.ReassignToUserId;
                }

                var activeTasks = await _context.Tasks
                    .Where(t => t.AssignedToId == command.UserId && t.Status != TaskStatusEnum.Complete)
                    .ToListAsync();

                foreach (var task in activeTasks)
                {
                    task.AssignedToId = command.ReassignToUserId;
                }

                await _context.SaveChangesAsync();

                var originalEmail = userToDelete.Email;
                var tombstone = $"deleted_{Guid.NewGuid():N}";

                userToDelete.IsDeleted = true;
                userToDelete.Email = $"{tombstone}_{originalEmail}";
                userToDelete.UserName = $"{tombstone}_{userToDelete.UserName}";
                userToDelete.LockoutEnabled = true;
                userToDelete.LockoutEnd = DateTimeOffset.MaxValue;
                userToDelete.UpdateAt = DateTime.UtcNow;

                await _userManager.UpdateSecurityStampAsync(userToDelete);
                var updateResult = await _userManager.UpdateAsync(userToDelete);

                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to update user {UserId} soft-delete state: {Errors}", command.UserId, errors);
                    throw new DataCorruptionException($"Failed to soft delete user '{command.UserId}'.");
                }

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "User {UserId} ({Email}) soft-deleted by admin {AdminId}. Reassigned: {CompCount} companies, {ContCount} contacts, {DealCount} deals, {TaskCount} tasks to user {TargetUserId}.",
                    command.UserId, originalEmail, currentUserId, companies.Count, contacts.Count, activeDeals.Count, activeTasks.Count, command.ReassignToUserId);

                return Result.Success(
                    message: "User deleted and operational resources reassigned successfully.",
                    statusCode: StatusCodes.Status200OK
                );
            }
            catch (Exception ex) when (ex is not DataCorruptionException)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected error occurred during soft deletion of user {UserId}.", command.UserId);
                throw;
            }
        }

        public async Task<Result> EditUserAsync(EditUserCommand command, Guid currentUserId)
        {
            var user = await _userManager.FindByIdAsync(command.UserId.ToString());

            if (user == null || user.IsDeleted)
            {
                _logger.LogWarning("User with ID {UserId} not found or is deleted.", command.UserId);
                return Result.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (!string.IsNullOrEmpty(command.FirstName))
            {
                user.FirstName = command.FirstName;
            }

            if (!string.IsNullOrEmpty(command.LastName))
            {
                user.LastName = command.LastName;
            }


            var updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to update user {UserId}: {Errors}", command.UserId, errors);
                throw new DataCorruptionException($"Failed to update user '{command.UserId}'. Errors: {errors}");
            }

            _logger.LogInformation("User {UserId} updated successfully by admin {AdminId}.", command.UserId, currentUserId);

            return Result.Success(
                message: "User updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ChangeUserEmailAsync(ChangeUserEmailCommand command, Guid adminId)
        {
            var user = await _userManager.FindByIdAsync(command.UserId.ToString());

            if (user == null || user.IsDeleted)
            {
                _logger.LogWarning("Admin {AdminId} attempted to change email for non-existent or deleted user {UserId}.",
                    adminId, command.UserId);

                return Result.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var normalizedNewEmail = command.NewEmail.Trim().ToUpperInvariant();

            if (string.Equals(user.NormalizedEmail, normalizedNewEmail, StringComparison.Ordinal))
            {
                _logger.LogInformation("New email is identical to current email for user {UserId}.", command.UserId);
                return Result.Failure(
                    message: "The new email address cannot be identical to the current one.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            bool emailOccupied = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => !u.IsDeleted &&
                               u.Id != user.Id &&
                               (u.NormalizedEmail == normalizedNewEmail ||
                                (u.PendingEmail != null && u.PendingEmail.ToUpper() == normalizedNewEmail)));

            if (emailOccupied)
            {
                _logger.LogWarning("Admin {AdminId} attempted to change user {UserId} email to an already occupied address: {Email}",
                    adminId, command.UserId, command.NewEmail);

                return Result.Failure(
                    message: "A user with this email address already exists or has a pending change.",
                    errorCode: ErrorCodes.UserAlreadyExists,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var targetNewEmail = command.NewEmail.Trim().ToLowerInvariant();
            user.PendingEmail = targetNewEmail;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to set PendingEmail for user {UserId}: {Errors}", user.Id, errors);
                throw new DataCorruptionException($"Failed to set PendingEmail for user '{user.Id}'. Errors: {errors}");
            }

            var token = await _userManager.GenerateChangeEmailTokenAsync(user, targetNewEmail);

            await _emailSender.SendEmailChangeConfirmationLinkAsync(new EmailChangeInitiatedDomain
            {
                UserId = user.Id,
                UserName = user.UserName ?? string.Empty,
                OldEmail = user.Email ?? string.Empty,
                NewEmail = targetNewEmail,
                Token = token
            });

            if (!string.IsNullOrEmpty(user.Email))
            {
                await _emailSender.SendEmailChangeSecurityAlertAsync(new EmailChangeAlertDomain
                {
                    UserName = user.UserName ?? string.Empty,
                    OldEmail = user.Email,
                    NewEmail = targetNewEmail
                });
            }

            _logger.LogInformation("Email change initiated for user {UserId} to '{NewEmail}' by admin {AdminId}.",
                user.Id, targetNewEmail, adminId);

            return Result.Success(
                message: "Email change initiated successfully. A confirmation link has been sent to the new email address.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ConfirmChangeUserEmailAsync(ConfirmChangeUserEmailCommand command)
        {
            var user = await _userManager.FindByIdAsync(command.UserId.ToString());

            if (user == null || user.IsDeleted)
            {
                _logger.LogWarning("Email change confirmation failed: User {UserId} not found or deleted.", command.UserId);
                return Result.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (string.IsNullOrEmpty(user.PendingEmail))
            {
                _logger.LogWarning("User {UserId} attempted to confirm email change, but has no pending email.", command.UserId);
                return Result.Failure(
                    message: "There is no pending email change request for this account.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var newEmail = user.PendingEmail;

            var result = await _userManager.ChangeEmailAsync(user, newEmail, command.Token);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("ChangeEmailAsync failed for user {UserId}. Errors: {Errors}", user.Id, errors);

                return Result.Failure(
                    message: "Invalid or expired confirmation token.",
                    errorCode: ErrorCodes.TokenInvalid,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            user.PendingEmail = null;
            await _userManager.UpdateSecurityStampAsync(user);
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Email successfully updated to '{NewEmail}' for user {UserId}.", newEmail, user.Id);

            return Result.Success(
                message: "Email address changed successfully. You can now use your new email to log in.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ForgotPasswordAsync(ForgotPasswordCommand command)
        {
            var user = await _userManager.FindByEmailAsync(command.Email);

            if (user == null || user.IsDeleted || !user.EmailConfirmed)
            {
                _logger.LogInformation("Password reset requested for non-existing, unconfirmed or deleted email: {Email}", command.Email);
                return Result.Success(
                    message: "If an account associated with this email exists, a password reset link has been sent.",
                    statusCode: StatusCodes.Status200OK
                );
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("Password reset requested for locked out user {UserId}.", user.Id);
                return Result.Failure(
                    message: "This account has been locked. Please contact support.",
                    errorCode: ErrorCodes.AccountLocked,
                    statusCode: StatusCodes.Status403Forbidden
                );
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            await _emailSender.SendPasswordResetEmailAsync(new ResetPasswordEmailDomain
            {
                UserId = user.Id,
                Email = user.Email!,
                UserName = user.UserName ?? string.Empty,
                Token = resetToken
            });

            _logger.LogInformation("Password reset email sent to user {UserId}.", user.Id);

            return Result.Success(
                message: "If an account associated with this email exists, a password reset link has been sent.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ResetPasswordAsync(ResetPasswordCommand command)
        {
            var user = await _userManager.FindByIdAsync(command.UserId.ToString());

            if (user == null || user.IsDeleted)
            {
                _logger.LogWarning("Password reset attempt for non-existent or deleted user {UserId}.", command.UserId);
                return Result.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var result = await _userManager.ResetPasswordAsync(user, command.Token, command.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to reset password for user {UserId}. Errors: {Errors}", command.UserId, errors);

                return Result.Failure(
                    message: "Invalid or expired password reset token.",
                    errorCode: ErrorCodes.TokenInvalid,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            _logger.LogInformation("Password successfully reset for user {UserId}.", user.Id);

            return Result.Success(
                message: "Password has been reset successfully. You can now log in with your new password.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ChangeRoleAsync(ChangeRoleCommand command, Guid adminId)
        {
            var user = await _userManager.FindByIdAsync(command.UserId.ToString());

            if (user == null || user.IsDeleted)
            {
                _logger.LogWarning("Admin {AdminId} attempted to change role for non-existent or deleted user {UserId}.",
                    adminId, command.UserId);
                return Result.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (user.Id == adminId)
            {
                _logger.LogWarning("Admin {AdminId} attempted to change their own role.", adminId);
                return Result.Failure(
                    message: "You cannot change your own role.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var currentRoles = await _userManager.GetRolesAsync(user);

            if (currentRoles.Count == 1 && string.Equals(currentRoles[0], command.Role, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Success(
                    message: "User already has this role.",
                    statusCode: StatusCodes.Status200OK
                );
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (currentRoles.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeResult.Succeeded)
                    {
                        var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
                        _logger.LogError("Failed to remove roles from user {UserId}: {Errors}", user.Id, errors);
                        throw new DataCorruptionException($"Failed to remove existing roles from user '{user.Id}'. Errors: {errors}");
                    }
                }

                var addResult = await _userManager.AddToRoleAsync(user, command.Role);
                if (!addResult.Succeeded)
                {
                    var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
                    _logger.LogError("Failed to assign new role '{Role}' to user {UserId}: {Errors}", command.Role, user.Id, errors);
                    throw new DataCorruptionException($"Failed to assign new role '{command.Role}' to user '{user.Id}'. Errors: {errors}");
                }

                await _userManager.UpdateSecurityStampAsync(user);

                await transaction.CommitAsync();
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Invalid operation during role assignment for user {UserId}: {Message}", user.Id, ex.Message);
                throw new DataCorruptionException($"Failed to assign role '{command.Role}' to user '{user.Id}'. Reason: {ex.Message}");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            _logger.LogInformation("User {UserId} role changed to '{NewRole}' by admin {AdminId}.", user.Id, command.Role, adminId);

            return Result.Success(
                message: "User role changed successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<UserDetailResponse>> GetUserDetailAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null || user.IsDeleted)
            {
                _logger.LogWarning("User with ID {UserId} not found or is deleted.", userId);
                return Result<UserDetailResponse>.Failure(
                    message: "User not found.",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            int companyOwnerCount = await _context.Companies.CountAsync(c => c.OwnerId == userId);
            int contactOwnerCount = await _context.Contacts.CountAsync(c => c.OwnerId == userId);
            int activeDealCount = await _context.Deals.CountAsync(d => d.OwnerId == userId 
                && (d.Status == DealsStatusEnum.ToDo || d.Status == DealsStatusEnum.InProgress));
           int activeTaskCount = await _context.Tasks.CountAsync(t => t.AssignedToId == userId 
                && (t.Status != TaskStatusEnum.Complete && t.Status != TaskStatusEnum.Break));

            var response = new UserDetailResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? throw new DataCorruptionException("User email is required"),
                PendingEmail = user.PendingEmail,
                IsEmailVerified = user.EmailConfirmed,

                IsLocked = user.LockoutEnabled 
                    && user.LockoutEnd.HasValue 
                    && user.LockoutEnd.Value > DateTime.UtcNow,

                LockoutEndDate = user.LockoutEnd?.UtcDateTime,
                
                CompanyOwnerCount = companyOwnerCount,
                ContactOwnerCount = contactOwnerCount,
                ActiveDealCount = activeDealCount,
                ActiveTaskCount = activeTaskCount,


                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdateAt
            };

            return Result<UserDetailResponse>.Success(
                message: "User details retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }
    }
}
