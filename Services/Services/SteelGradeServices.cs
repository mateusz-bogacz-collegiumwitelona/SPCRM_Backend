using Domain.Common;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Interfaces;
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
    }
}
