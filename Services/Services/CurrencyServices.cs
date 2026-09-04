using Domain.Common;
using Domain.Constants;
using Domain.Exceptions.Exception;
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
            var currencies = await _context.Currencies
                .AsNoTracking()
                .OrderBy(c => c.Code)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Code,
                    c.DecimalPlaces
                })
                .ToListAsync();

            var corruptedCurrency = currencies.FirstOrDefault(c =>
                string.IsNullOrWhiteSpace(c.Code) ||
                string.IsNullOrWhiteSpace(c.Name) ||
                c.DecimalPlaces < 0);

            if (corruptedCurrency != null)
            {
                _logger.LogError("Critical data corruption: Currency {CurrencyId} has invalid code, name or decimal places.", corruptedCurrency.Id);
                throw new DataCorruptionException($"Currency configuration for '{corruptedCurrency.Id}' is corrupted.");
            }

            var response = currencies.Select(c => new CurrencyListResponse
            {
                CurrencyId = c.Id,
                Name = c.Name,
                Code = c.Code,
                DecimalPlace = c.DecimalPlaces
            }).ToList();

            return Result<List<CurrencyListResponse>>.Success(
                message: "Currency list retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
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
                _logger.LogInformation("Currency with id {CurrencyId} not found.", command.CurrencyId);
                return Result.Failure(
                    message: "Currency not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.CurrencyNotFound
                );
            }

            if (string.IsNullOrWhiteSpace(currency.Code) || string.IsNullOrWhiteSpace(currency.Name) || currency.DecimalPlaces < 0)
            {
                _logger.LogError("Critical data corruption: Currency {CurrencyId} in database has invalid state.", currency.Id);
                throw new DataCorruptionException($"Currency '{currency.Id}' has corrupted configuration.");
            }

            var isNameChanged = !string.IsNullOrEmpty(command.Name) && !string.Equals(currency.Name, command.Name, StringComparison.OrdinalIgnoreCase);
            var isCodeChanged = !string.IsNullOrEmpty(command.Code) && !string.Equals(currency.Code, command.Code, StringComparison.OrdinalIgnoreCase);

            if (isNameChanged || isCodeChanged)
            {
                var normalizedName = command.Name?.ToLower();
                var normalizedCode = command.Code?.ToUpper();

                var conflict = await _context.Currencies
                    .AsNoTracking()
                    .Where(c => c.Id != command.CurrencyId &&
                                ((isNameChanged && c.Name.ToLower() == normalizedName) ||
                                 (isCodeChanged && c.Code.ToUpper() == normalizedCode)))
                    .Select(c => new
                    {
                        HasSameName = isNameChanged && c.Name.ToLower() == normalizedName,
                        HasSameCode = isCodeChanged && c.Code.ToUpper() == normalizedCode
                    })
                    .FirstOrDefaultAsync();

                if (conflict != null)
                {
                    if (conflict.HasSameName)
                    {
                        _logger.LogWarning("Currency with name {Name} already exists.", command.Name);
                        return Result.Failure(
                            message: "Currency with this name already exists.",
                            statusCode: StatusCodes.Status409Conflict,
                            errorCode: ErrorCodes.CurrencyNameAlreadyExists
                        );
                    }

                    if (conflict.HasSameCode)
                    {
                        _logger.LogWarning("Currency with code {Code} already exists.", command.Code);
                        return Result.Failure(
                            message: "Currency with this code already exists.",
                            statusCode: StatusCodes.Status409Conflict,
                            errorCode: ErrorCodes.CurrencyCodeAlreadyExists
                        );
                    }
                }
            }

            if (isNameChanged)
            {
                currency.Name = command.Name!;
            }

            if (isCodeChanged)
            {
                currency.Code = command.Code!;
            }

            if (command.DecimalPlaces.HasValue)
            {
                currency.DecimalPlaces = command.DecimalPlaces.Value;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Currency {CurrencyId} updated successfully.", command.CurrencyId);

            return Result.Success(
                message: "Currency updated successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }
    }
}
