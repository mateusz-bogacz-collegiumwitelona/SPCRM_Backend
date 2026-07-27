using Domain.Common;
using Domain.Constants;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Helpers;
using Services.Interfaces;
using Services.Response;

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
                .Where(n => n.ContactId == command.searchId && !n.IsDeleted)
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
                    UpdateAt = n.UpdateAt
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
                .FirstOrDefaultAsync(n => n.Id == command.Id && !n.IsDeleted);

            if (note == null)
            {
                _logger.LogWarning("Note with ID {NoteId} not found or is deleted.", command.Id);

                return Result.Failure(
                    message: "Note not found or is deleted",
                    errorCode: ErrorCodes.NoteNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            } 

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found.", userId);
                return Result.Failure(
                    message: "User not found",
                    errorCode: ErrorCodes.UserNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (user.Id != note.AuthorId && !await _userManager.IsInRoleAsync(user, "Manager"))
            {
                _logger.LogWarning("User with ID {UserId} is not authorized to edit note with ID {NoteId}.", userId, command.Id);
                return Result.Failure(
                    message: "You are not authorized to edit this note",
                    errorCode: ErrorCodes.UnauthorizedAccess,
                    statusCode: StatusCodes.Status403Forbidden
                );
            }

            if (!string.IsNullOrWhiteSpace(command.Title)) note.Title = command.Title;
            if (!string.IsNullOrWhiteSpace(command.Content)) note.Content = command.Content;

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Note updated successfully",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
