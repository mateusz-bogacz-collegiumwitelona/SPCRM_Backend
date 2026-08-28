using Domain.Common;
using Domain.Constants;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Currency;
using Services.Command.List;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Currency;

namespace Services.Services
{
    public class CurrencyServices : ICurrencyServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CurrencyServices> _logger;

        public CurrencyServices(AppDbContext context, ILogger<CurrencyServices> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Result<List<CurrencyListResponse>>> GetCurrencySimpleListAsync()
        {
            var query = await _context.Currencies
                .Select(c => new CurrencyListResponse
                {
                    CurrencyId = c.Id,
                    Name = c.Name,
                    Code = c.Code,
                    DecimalPlace = c.DecimalPlaces
                }).ToListAsync();

            return Result<List<CurrencyListResponse>>.Success(
               message: "Currency list retrieved successfully.",
               statusCode: StatusCodes.Status200OK,
               data: query
               );
        }

        public async Task<Result<PagedResult<CurrencyListResponse>>> GetCurrenyListAsync(BasicListCommand command)
            => await _context.Currencies
                    .AsNoTracking()
                    .ApplySearch(command.SearchTerm ?? string.Empty)
                    .ApplySorting(command.SortBy, command.SortDescending)
                    .Select(c => new CurrencyListResponse
                    {
                        CurrencyId = c.Id,
                        Name = c.Name,
                        Code = c.Code,
                        DecimalPlace = c.DecimalPlaces
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "currency");

        public async Task<Result> AddCurrencyAsync(AddCurrencyCommand command)
        {
            var isExist = await _context.Currencies.AnyAsync(c =>
                c.Code == command.Code ||
                c.Name.ToLower() == command.Name.ToLower()
            );

            if (isExist)
            {
                _logger.LogWarning("Currency with code {Code} or name {Name} already exists.", command.Code, command.Name);
                return Result.Failure(
                    message: "Currency already exists.",
                    statusCode: StatusCodes.Status400BadRequest,
                    errorCode: ErrorCodes.CurrencyAlreadyExists
                );
            }

            var currency = new Currency
            {
                Name = command.Name,
                Code = command.Code,
                DecimalPlaces = command.DecimalPlaces
            };

            _context.Currencies.Add(currency);
            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Currency added successfully.",
                statusCode: StatusCodes.Status201Created
            );
        }

        public async Task<Result> EditCurrencyAsync(EditCurrencyCommand command)
        {
            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == command.CurrencyId);

            if (currency == null)
            {
                _logger.LogWarning("Currency with id {id} not found.", command.CurrencyId);
                return Result.Failure(
                    message: "Currency not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CurrencyNotFound
                );
            }

            if (!string.IsNullOrEmpty(command.Name))
            {
                var isNameExist = await _context.Currencies.AnyAsync(c => c.Name.ToLower() == command.Name.ToLower() && c.Id != command.CurrencyId);

                if (isNameExist)
                {
                    _logger.LogWarning("Currency with name {Name} already exists.", command.Name);
                    return Result.Failure(
                        message: "Currency with this name already exists.",
                        statusCode: StatusCodes.Status409Conflict,
                        errorCode: ErrorCodes.CurrencyNameAlreadyExists
                        );
                }

                currency.Name = command.Name;
            }

            if (!string.IsNullOrEmpty(command.Code))
            {
                var isCodeExist = await _context.Currencies.AnyAsync(c => c.Code == command.Code && c.Id != command.CurrencyId);
                if (isCodeExist)
                {
                    _logger.LogWarning("Currency with code {Code} already exists.", command.Code);
                    return Result.Failure(
                        message: "Currency with this code already exists.",
                        statusCode: StatusCodes.Status409Conflict,
                        errorCode: ErrorCodes.CurrencyCodeAlreadyExists
                        );
                }
                currency.Code = command.Code;
            }

            if (command.DecimalPlaces.HasValue)
            {
                currency.DecimalPlaces = command.DecimalPlaces.Value;
            }

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Currency updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
