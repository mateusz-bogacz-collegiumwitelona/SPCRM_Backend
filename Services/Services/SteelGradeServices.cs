using Domain.Common;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response;

namespace Services.Services
{
    public class SteelGradeServices : ISteelGradeServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SteelGradeServices> _logger;

        public SteelGradeServices(AppDbContext context, ILogger<SteelGradeServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<IEnumerable<SteelGradeResponse>>> GetSteelGradesAsync()
        {
            var query = await _context.SteelGrades
                .OrderBy(s => s.Name)
                .Select(s => new SteelGradeResponse
                {
                    Id = s.Id,
                    Name = s.Name
                })
                .ToListAsync();

            return Result<IEnumerable<SteelGradeResponse>>.Success(
                message: "Steel grades retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: query
                );
        }

        public async Task<Result<PagedResult<SteelGradeListResponse>>> GetSteelGradeList(SteelGradeListCommand command)
        {
            var query = _context.SteelGrades
                .AsNoTracking()
                .ApplySeatch(command.SearchTerm ?? string.Empty)
                .ApplySorting(command.SortBy, command.SortDescending)
                .Select(st => new SteelGradeListResponse
                {
                    Id = st.Id,
                    Name = st.Name,
                    Standard = st.Standard,
                    Density = st.Density / 1000m
                });

            return await query.ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "steel-grade");
        }
    }
}
