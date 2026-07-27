using Domain.Common;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
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

        public NoteServices(AppDbContext context, ILogger<NoteServices> logger)
        {
            _context = context;
            _logger = logger;
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
    }
}
