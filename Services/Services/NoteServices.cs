using Domain.Common;
using Domain.Constants;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Note;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Note;

namespace Services.Services
{
    public class NoteServices : INoteServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NoteServices> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public NoteServices(
            AppDbContext context,
            ILogger<NoteServices> logger,
            UserManager<ApplicationUser> roleManger

            )
        {
            _context = context;
            _logger = logger;
            _userManager = roleManger;
        }

        public async Task<Result<PagedResult<ContactNoteResponse>>> GetContactNoteAsync(NoteListCommand command)
        {
            var query = _context.Notes
                .OfType<ContactNote>()
                .Include(n => n.Author)
                .Where(n => n.ContactId == command.SearchId)
                .AsNoTracking()
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new ContactNoteResponse
                {
                    Id = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    AuthorFirstName = n.Author.FirstName,
                    AuthorLastName = n.Author.LastName,
                    CreatedAt = n.CreatedAt,
                    UpdateAt = n.UpdateAt,
                    AuthorId = n.Author.Id,
                });

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "contact_notes");
        }

        public async Task<Result<List<NoteResponse>>> GetDealNotesAsync(Guid dealId)
        {
            var query = await _context.Notes
                .OfType<DealNote>()
                .AsNoTracking()
                .Where(n => n.DealId == dealId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NoteResponse
                {
                    NoteId = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    AuthorFirstName = n.Author.FirstName,
                    AuthorLastName = n.Author.LastName,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdateAt ?? null,
                    AuthorId = n.Author.Id,
                })
                .ToListAsync();

            return Result<List<NoteResponse>>.Success(
                data: query,
                message: "Deal notes retrieved successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<List<NoteResponse>>> GetTaskNotesAsync(Guid taskId)
        {
            bool isTaskExists = await _context.Tasks
                .AsNoTracking()
                .AnyAsync(t => t.Id == taskId);

            if (!isTaskExists)
            {
                return Result<List<NoteResponse>>.Failure(
                    message: "Task for this note not found",
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var query = await _context.Tasks
                .AsNoTracking()
                .Where(t => t.Id == taskId)
                .SelectMany(t => t.Notes)
                .Where(n => !n.IsDeleted)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NoteResponse
                {
                    NoteId = n.Id,
                    Title = n.Title,
                    Content = n.Content,
                    AuthorFirstName = n.Author.FirstName,
                    AuthorLastName = n.Author.LastName,
                    CreatedAt = n.CreatedAt,
                    UpdatedAt = n.UpdateAt ?? null,
                    AuthorId = n.Author.Id,
                })
                .ToListAsync();

            return Result<List<NoteResponse>>.Success(
                data: query,
                message: "Task notes retrieved successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> EditNoteAsync(NoteEditCommand command, Guid userId)
        {
            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == command.Id);

            if (note == null)
            {
                _logger.LogInformation("Note with ID {NoteId} not found.", command.Id);
                return Result.Failure(
                    message: "Note not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NoteNotFound
                );
            }

            if (note.AuthorId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Note {NoteId} has no assigned author.", note.Id);
                throw new DataCorruptionException($"Note '{note.Id}' has no valid author.");
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

            if (user == null)
            {
                _logger.LogError("Security/Integrity violation: User with ID {UserId} does not exist.", userId);
                throw new UserNotFoundException(userId);
            }

            var isAuthor = user.Id == note.AuthorId;
            var isManager = await _userManager.IsInRoleAsync(user, "Manager") || await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAuthor && !isManager)
            {
                _logger.LogWarning("Security violation: User {UserId} attempted to edit note {NoteId} owned by {AuthorId}.", userId, command.Id, note.AuthorId);
                throw new ForbiddenException("You are not authorized to edit this note.");
            }

            if (!string.IsNullOrWhiteSpace(command.Title)) note.Title = command.Title.Trim();
            if (!string.IsNullOrWhiteSpace(command.Content)) note.Content = command.Content.Trim();

            note.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Note {NoteId} updated successfully by user {UserId}.", note.Id, userId);

            return Result.Success(
                message: "Note updated successfully",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> AddNoteAsync(NoteAddCommand command)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == command.AuthorId && !u.IsDeleted);

            if (user == null)
            {
                _logger.LogError("Security/Integrity violation: User with ID {UserId} does not exist or is deleted.", command.AuthorId);
                throw new UserNotFoundException(command.AuthorId);
            }

            if (!Enum.IsDefined(typeof(NoteEnum), command.NoteType))
            {
                _logger.LogWarning("Invalid note type provided: {NoteType}.", command.NoteType);
                return Result.Failure(
                    message: "Invalid note type provided.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.InvalidOperation
                );
            }

            bool targetExists = command.NoteType switch
            {
                NoteEnum.Contact => await _context.Contacts.AsNoTracking().AnyAsync(c => c.Id == command.TargetId),
                NoteEnum.Deal => await _context.Deals.AsNoTracking().AnyAsync(d => d.Id == command.TargetId),
                NoteEnum.Task => await _context.Tasks.AsNoTracking().AnyAsync(t => t.Id == command.TargetId),
                _ => false
            };

            if (!targetExists)
            {
                _logger.LogInformation("Target entity {TargetType} with ID {TargetId} not found.", command.NoteType, command.TargetId);
                return Result.Failure(
                    message: $"{command.NoteType} for this note not found",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NoteTargetNotFound
                );
            }

            Note newNote = command.NoteType switch
            {
                NoteEnum.Contact => new ContactNote
                {
                    ContactId = command.TargetId,
                    Title = command.Title.Trim(),
                    Content = command.Content.Trim(),
                    Author = user
                },
                NoteEnum.Deal => new DealNote
                {
                    DealId = command.TargetId,
                    Title = command.Title.Trim(),
                    Content = command.Content.Trim(),
                    Author = user
                },
                NoteEnum.Task => new TaskNote
                {
                    TaskId = command.TargetId,
                    Title = command.Title.Trim(),
                    Content = command.Content.Trim(),
                    Author = user
                },
                _ => throw new InvalidOperationException($"Unhandled note type: {command.NoteType}")
            };

            _context.Notes.Add(newNote);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Note {NoteId} added to {TargetType} ({TargetId}) by user {AuthorId}.", newNote.Id, command.NoteType, command.TargetId, command.AuthorId);

            return Result.Success(
                message: "Note added successfully",
                statusCode: StatusCodes.Status201Created
            );
        }

        public async Task<Result> DeleteNoteAsync(Guid noteId, Guid userId)
        {
            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == noteId);

            if (note == null)
            {
                _logger.LogInformation("Note with ID {NoteId} not found or is already deleted.", noteId);
                return Result.Failure(
                    message: "Note not found or is already deleted",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.NoteNotFound
                );
            }

            if (note.AuthorId == Guid.Empty)
            {
                _logger.LogError("Critical data corruption: Note {NoteId} has no assigned author.", note.Id);
                throw new DataCorruptionException($"Note '{note.Id}' has no valid author.");
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null || user.IsDeleted)
            {
                _logger.LogError("Security/Integrity violation: User with ID {UserId} does not exist or is deleted.", userId);
                throw new UserNotFoundException(userId);
            }

            var isAuthor = note.AuthorId == userId;
            var isPrivileged = await _userManager.IsInRoleAsync(user, "Manager") || await _userManager.IsInRoleAsync(user, "Admin");

            if (!isAuthor && !isPrivileged)
            {
                _logger.LogWarning("Security violation: User {UserId} attempted to delete note {NoteId} owned by {AuthorId}.", userId, noteId, note.AuthorId);
                throw new ForbiddenException("You are not authorized to delete this note.");
            }

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Note {NoteId} deleted successfully by user {UserId}.", noteId, userId);

            return Result.Success(
                message: "Note deleted successfully",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
