using Domain.Common;
using Domain.Constants;
using Domain.Enum;
using Domain.Events;
using Domain.Exceptions.Exception;
using Domain.Models;
using Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Command.Company;
using Services.Command.Deal;
using Services.Command.List;
using Services.Command.Product;
using Services.Factory.Interfaces;
using Services.Helpers;
using Services.Interfaces;
using Services.QueryExtension;
using Services.Response.Company;
using Services.Response.Deal;

namespace Services.Services
{
    public class DealServices : IDealServices
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DealServices> _logger;
        private readonly IEntityAuthorizationService _entityAuth;
        private readonly IDealStateMachineFactory _state;
        private readonly IInventoryService _inventory;
        private readonly IPublisher _publisher;

        public DealServices(
            AppDbContext context,
            ILogger<DealServices> logger,
            IEntityAuthorizationService entityAuth,
            IDealStateMachineFactory state,
            IInventoryService inventory,
            IPublisher publisher
            )
        {
            _context = context;
            _logger = logger;
            _entityAuth = entityAuth;
            _state = state;
            _inventory = inventory;
            _publisher = publisher;
        }

        public async Task<Result<PagedResult<UserDealResponse>>> GetDealsAsync(DealListCommand command, Guid? forcedOwnerId = null)
        {
            var effectiveOwnerId = forcedOwnerId ?? command.OwnerId;

            var query = await _context.Deals
                       .AsNoTracking()
                       .ApplyFilter(
                           command.CompanyName,
                           command.Value,
                           command.DateFrom,
                           command.DateTo,
                           command.StatusType,
                           effectiveOwnerId
                       )
                       .ApplySorting(command.SortBy, command.SortDescending)
                       .ApplySearch(command.SearchTerm ?? string.Empty)
                       .Select(d => new UserDealResponse
                       {
                           Id = d.Id,
                           Name = d.Name,
                           Nip = d.Company.NIP,
                           CompanyName = d.Company.Name,
                           CloseDate = d.CloseDate,
                           Value = d.Value,
                           DecimalPlace = d.Currency.DecimalPlaces,
                           Currency = d.Currency.Code,
                           Status = d.Status.ToString(),
                           OwnerId = d.OwnerId,
                           OwnerFirstName = d.Owner.FirstName,
                           OwnerLastName = d.Owner.LastName
                       })
                       .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "sales");

            return query;
        }

        public async Task<Result<List<string>>> GetDealsStatus()
            => Result<List<string>>.Success(
                message: "Deals statuses retrieved successfully",
                statusCode: StatusCodes.Status200OK,
                data: Enum.GetNames(typeof(DealsStatusEnum)).ToList()
                );

        public async Task<Result<PagedResult<CompanyDealsResponse>>> GetComapanyDealsAsync(CompanyCommand command)
            => await _context.Deals
                    .Where(d => d.CompanyId == command.CompanyId)
                    .Select(d => new CompanyDealsResponse
                    {
                        Id = d.Id,
                        SalesmanFirstName = d.Owner.FirstName,
                        SalesmanLastName = d.Owner.LastName,
                        Name = d.Name,
                        Value = (decimal)d.Value / 10000m,
                        Code = d.Currency.Code,
                        DecimalPlaces = d.Currency.DecimalPlaces,
                        Status = d.Status.ToString(),
                        CloseDate = d.CloseDate,
                        CreatedAt = d.CreatedAt
                    })
                    .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "company_sales");

        public async Task<Result<DealDetailResponse>> GetDealDetailAsync(Guid dealId, Guid currentUserId)
        {
            var now = DateTime.UtcNow;

            var query = await (
                from d in _context.Deals.AsNoTracking()
                where d.Id == dealId
                join curr in _context.Currencies.AsNoTracking() on d.CurrencyId equals curr.Id into currGroup
                from curr in currGroup.DefaultIfEmpty()
                join u in _context.Users.AsNoTracking() on d.OwnerId equals u.Id into uGroup
                from u in uGroup.DefaultIfEmpty()
                join comp in _context.Companies.AsNoTracking() on d.CompanyId equals comp.Id into compGroup
                from comp in compGroup.DefaultIfEmpty()
                join ct in _context.Contacts.AsNoTracking() on d.ContactId equals ct.Id into ctGroup
                from ct in ctGroup.DefaultIfEmpty()
                select new
                {
                    DealExists = true,
                    d.Id,
                    d.Name,
                    d.Value,
                    Status = d.Status.ToString(),
                    d.CloseDate,

                    d.CurrencyId,
                    HasCurrency = curr != null,
                    CurrencyCode = curr != null ? curr.Code : null,
                    DecimalPlaces = curr != null ? (int?)curr.DecimalPlaces : null,

                    d.OwnerId,
                    HasOwner = u != null,
                    OwnerFirstName = u != null ? u.FirstName : null,
                    OwnerLastName = u != null ? u.LastName : null,

                    d.CompanyId,
                    HasCompany = comp != null,
                    CompanyName = comp != null ? comp.Name : null,

                    d.ContactId,
                    ContactFirstName = ct != null ? ct.FirstName : null,
                    ContactLastName = ct != null ? ct.LastName : null,

                    InvoicedAmount = d.Invoices.Sum(i => (long?)i.TotalAmount) ?? 0,
                    PaidAmount = d.Invoices.Sum(i => (long?)i.PaidAmount) ?? 0,
                    IsOverdueInvoices = d.Invoices.Any(i =>
                        (i.TotalAmount - i.PaidAmount) > 0 &&
                        i.DueDate < now
                    )
                }
            ).FirstOrDefaultAsync();

            if (query == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found.", dealId);
                return Result<DealDetailResponse>.Failure(
                    message: "Deal not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.DealNotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(currentUserId, query.OwnerId);

            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized access to Deal {DealId}.", currentUserId, dealId);
                throw new ForbiddenException("You are not authorized to modify this company.");
            }

            if (!query.HasCurrency || !query.HasOwner || !query.HasCompany ||
                string.IsNullOrWhiteSpace(query.CurrencyCode) ||
                !query.DecimalPlaces.HasValue)
            {
                _logger.LogError("Critical data corruption: Sale {DealId} has missing Currency, Owner, or Company linkage.", dealId);
                throw new DataCorruptionException($"Deal '{dealId}' contains corrupted relational linkages.");
            }

            if (query.Value < 0 || query.InvoicedAmount < 0 || query.PaidAmount < 0)
            {
                _logger.LogError("Critical data corruption: Deal {DealId} contains negative monetary values (Value: {Value}, Invoiced: {Invoiced}, Paid: {Paid}).",
                    dealId, query.Value, query.InvoicedAmount, query.PaidAmount);
                throw new DataCorruptionException($"Deal '{dealId}' contains corrupted financial amounts.");
            }

            var paymentPercentage = query.Value > 0
                ? (int)Math.Round(
                    (decimal)query.PaidAmount / query.Value * 100m,
                    MidpointRounding.AwayFromZero)
                : 0;

            var response = new DealDetailResponse
            {
                Id = query.Id,
                Name = query.Name,
                Value = query.Value,
                Status = query.Status,
                CloseDate = query.CloseDate,
                CurrencyCode = query.CurrencyCode,
                DecimalPlaces = query.DecimalPlaces.Value,
                OwnerFirstName = query.OwnerFirstName ?? string.Empty,
                OwnerLastName = query.OwnerLastName ?? string.Empty,
                CompanyName = query.CompanyName ?? string.Empty,
                ContactId = query.ContactId,
                ContactFirstName = query.ContactFirstName ?? string.Empty,
                ContactLastName = query.ContactLastName ?? string.Empty,
                InvoicedAmount = query.InvoicedAmount,
                PaidAmount = query.PaidAmount,
                IsOverduelInvoices = query.IsOverdueInvoices,
                PaymentPercentage = paymentPercentage
            };

            return Result<DealDetailResponse>.Success(
                message: "Deal detail retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: response
            );
        }

        public async Task<Result<PagedResult<DealProductResponse>>> GetDealProductAsync(
            Guid dealId,
            ProductListCommand command,
            Guid currentUserId)
        {
            var dealOwnerId = await _context.Deals
                .AsNoTracking()
                .Where(d => d.Id == dealId)
                .Select(d => (Guid?)d.OwnerId)
                .FirstOrDefaultAsync();

            if (!dealOwnerId.HasValue)
            {
                _logger.LogInformation("Sale with ID {DealId} not found when fetching products.", dealId);
                return Result<PagedResult<DealProductResponse>>.Failure(
                    message: "Sale not found.",
                    statusCode: StatusCodes.Status404NotFound,
                    errorCode: ErrorCodes.DealNotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(currentUserId, dealOwnerId.Value);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized access to products of Deal {DealId}.", currentUserId, dealId);
                throw new ForbiddenException("You don't have access to this");
            }

            return await _context.DealProducts
                .AsNoTracking()
                .Where(dp => dp.DealId == dealId)
                .ApplySearch(command.SearchTerm ?? string.Empty)
                .ApplyFilter(command.ProductCategory, command.SteelGrade)
                .ApplySorting(command.SortBy, command.SortDescending)
                .Select(dp => new DealProductResponse
                {
                    DealProductId = dp.Id,
                    ProductId = dp.ProductId,
                    Name = dp.Product.Name,
                    SteelGrade = dp.Product.SteelGrade.Name,
                    Dimensions = DimensionsFormatter.Format(
                        dp.Product.Category,
                        dp.Product.Diameter,
                        dp.Product.Thickness,
                        dp.Product.Width,
                        dp.Product.Length
                    ),
                    Quantity = dp.Quantity,
                    UnitSymbol = dp.Product.Unit.Symbol,
                    BaseUnitPrice = dp.Product.PricePerUnit,
                    UnitPrice = dp.UnitPrice,
                    TotalPrice = dp.Quantity * dp.UnitPrice,
                    CurrencyCode = dp.Deal.Currency.Code,
                    DecimalPlaces = dp.Deal.Currency.DecimalPlaces
                })
                .ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "deal_products");
        }

        public async Task<Result> AddDealAsync(AddDealCommand command, Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found when attempting to add a deal.", userId);
                throw new UserNotFoundException(userId);
            }

            var company = await _context.Companies.FindAsync(command.CompanyId);

            if (company == null)
            {
                _logger.LogWarning("Company with ID {CompanyId} not found when attempting to add a deal.", command.CompanyId);
                return Result.Failure(
                    message: "Company does not exist.",
                    errorCode: ErrorCodes.CompanyNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var currencyExists = await _context.Currencies.AnyAsync(c => c.Id == command.CurrencyId);
            if (!currencyExists)
            {
                _logger.LogWarning("Currency with ID {CurrencyId} not found when attempting to add a deal.", command.CurrencyId);
                return Result.Failure(
                    message: "Currency does not exist.",
                    errorCode: ErrorCodes.CurrencyNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (command.Products == null || !command.Products.Any())
            {
                return Result.Failure(
                    message: "Deal must contain at least one product.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var hasInvalidItems = command.Products.Any(p => p.Quantity <= 0 || p.UnitPrice < 0 || p.ProductId == Guid.Empty);
            if (hasInvalidItems)
            {
                return Result.Failure(
                    message: "All products must have positive quantity and non-negative unit price.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var requestedProductIds = command.Products.Select(p => p.ProductId).Distinct().ToList();
            var existingProductIds = await _context.Products
                .AsNoTracking()
                .Where(p => requestedProductIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            var missingProducts = requestedProductIds.Except(existingProductIds).ToList();
            if (missingProducts.Any())
            {
                return Result.Failure(
                    message: "One or more products specified do not exist.",
                    errorCode: ErrorCodes.ProductNotFound,
                    statusCode: StatusCodes.Status404NotFound,
                    errors: missingProducts.Select(id => $"Product with ID {id} does not exist.").ToList()
                );
            }

            foreach (var product in command.Products)
            {
                var stockValidation = await _inventory.ValidateStockAvailabilityAsync(product.ProductId, product.Quantity);
                if (!stockValidation.IsSuccess)
                {
                    return stockValidation;
                }
            }

            var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == command.ContactId);

            if (contact == null)
            {
                _logger.LogWarning("Contact with ID {ContactId} not found when attempting to add a deal.", command.ContactId);
                return Result.Failure(
                    message: "Contact not found.",
                    errorCode: ErrorCodes.ContactNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (contact.CompanyId != company.Id)
            {
                _logger.LogWarning("Contact ID {ContactId} does not belong to Company ID {CompanyId}.", contact.Id, company.Id);
                return Result.Failure(
                    message: "Contact does not belong to the specified company.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var totalValue = command.Products.Sum(p => (long)p.Quantity * p.UnitPrice);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var countToday = await _context.Deals
                    .CountAsync(d => d.CreatedAt >= today && d.CreatedAt < tomorrow);

                var dealName = $"D/{today:yyyy/MM/dd}/{(countToday + 1):D4}";

                var deal = new Deal
                {
                    Name = dealName,
                    Value = totalValue,
                    Status = DealsStatusEnum.ToDo,
                    CloseDate = DateTime.SpecifyKind(command.CloseDate, DateTimeKind.Utc),
                    CurrencyId = command.CurrencyId,
                    CompanyId = command.CompanyId,
                    ContactId = contact.Id,
                    OwnerId = userId,
                    DealProducts = command.Products.Select(p => new DealProduct
                    {
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,
                        UnitPrice = p.UnitPrice
                    }).ToList()
                };

                _context.Deals.Add(deal);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("New deal with ID {DealId} added successfully by user {UserId}.", deal.Id, userId);

                return Result.Success(
                    message: "Deal added successfully.",
                    statusCode: StatusCodes.Status201Created
                );
            }
            catch
            {
                await transaction.RollbackAsync();
                _logger.LogError("An error occurred while adding a new deal. Transaction rolled back.");
                throw;
            }
        }

        public async Task<Result> DeleteDealAsync(Guid userId, Guid dealId)
        {
            var deal = await _context.Deals.FirstOrDefaultAsync(d => d.Id == dealId);

            if (deal == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found for deletion.", dealId);
                return Result.Failure(
                    message: "Deal not found.",
                    errorCode: ErrorCodes.DealNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(userId, deal.OwnerId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized deletion of Deal {DealId}.", userId, dealId);
                throw new ForbiddenException("You are not authorized to delete this deal.");
            }

            var stateMachine = _state.Create(deal);
            var transitionResult = stateMachine.TransitionTo(DealsStatusEnum.Cancelled);

            if (!transitionResult.IsSuccess)
            {
                _logger.LogWarning("Deal {DealId} cannot be deleted due to its current status: {Status}.", dealId, deal.Status);
                return transitionResult;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deal {DealId} marked as Cancelled by User {UserId}.", dealId, userId);

            return Result.Success(
                message: "Deal deleted successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> ExtendDealCloseDateAsync(ExtendDealCloseDateCommand command, Guid userId)
        {
            var deal = await _context.Deals.FirstOrDefaultAsync(d => d.Id == command.DealId);

            if (deal == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found for extending close date.", command.DealId);
                return Result.Failure(
                    message: "Deal not found.",
                    errorCode: ErrorCodes.DealNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(userId, deal.OwnerId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized close date extension of Deal {DealId}.", userId, command.DealId);
                throw new ForbiddenException("You are not authorized to modify this deal.");
            }

            var stateMachine = _state.Create(deal);
            var canModify = stateMachine.CanModify();
            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("User {UserId} attempted to extend finalized Deal {DealId} with status {Status}.", userId, command.DealId, deal.Status);
                return canModify;
            }

            var targetCloseDate = DateTime.SpecifyKind(command.NewCloseDate, DateTimeKind.Utc);

            if (targetCloseDate <= deal.CloseDate)
            {
                _logger.LogWarning("User {UserId} attempted to extend Deal {DealId} to a date earlier than or equal to current: {NewCloseDate}.", userId, command.DealId, targetCloseDate);
                return Result.Failure(
                    message: "New close date must be later than the current close date.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            deal.CloseDate = targetCloseDate;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deal {DealId} close date extended to {NewCloseDate} by User {UserId}.", command.DealId, targetCloseDate, userId);

            return Result.Success(
                message: "Deal close date extended successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> AddDealProductAsync(Guid dealId, AddDealProductCommand command, Guid userId)
        {
            var deal = await _context.Deals
                .Include(d => d.DealProducts)
                .FirstOrDefaultAsync(d => d.Id == dealId);

            if (deal == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found when attempting to add products.", dealId);
                return Result.Failure(
                    message: "Deal not found.",
                    errorCode: ErrorCodes.DealNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(userId, deal.OwnerId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized modification of Deal {DealId}.", userId, dealId);
                throw new ForbiddenException("You are not authorized to modify this deal.");
            }

            var stateMachine = _state.Create(deal);
            var canModify = stateMachine.CanModify();
            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Cannot add products to Deal {DealId} due to status: {Status}.", dealId, deal.Status);
                return canModify;
            }

            if (deal.DealProducts.Any(dp => dp.ProductId == command.ProductId))
            {
                _logger.LogInformation("Product with ID {ProductId} already exists in Deal {DealId}.", command.ProductId, dealId);
                return Result.Failure(
                    message: "Product already exists in the deal.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var productExists = await _context.Products.AnyAsync(p => p.Id == command.ProductId);
            if (!productExists)
            {
                _logger.LogInformation("Product with ID {ProductId} not found when attempting to add to Deal {DealId}.", command.ProductId, dealId);
                return Result.Failure(
                    message: "Product not found.",
                    errorCode: ErrorCodes.ProductNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var stockValidation = await _inventory.ValidateStockAvailabilityAsync(command.ProductId, command.Quantity);
            if (!stockValidation.IsSuccess)
            {
                return stockValidation;
            }

            var dealProduct = new DealProduct
            {
                DealId = dealId,
                ProductId = command.ProductId,
                Quantity = command.Quantity,
                UnitPrice = command.UnitPrice
            };

            _context.DealProducts.Add(dealProduct);

            deal.Value = deal.DealProducts.Sum(dp => (long)dp.Quantity * dp.UnitPrice);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Product {ProductId} added to Deal {DealId} by User {UserId}.", command.ProductId, dealId, userId);

            return Result.Success(
                message: "Product added to deal successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> DeleteDealProductAsync(Guid dealId, Guid dealProductId, Guid userId)
        {
            var deal = await _context.Deals
                .Include(d => d.DealProducts)
                .FirstOrDefaultAsync(d => d.Id == dealId);

            if (deal == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found when attempting to remove product.", dealId);
                return Result.Failure(
                    message: "Deal not found.",
                    errorCode: ErrorCodes.DealNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(userId, deal.OwnerId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized modification of Deal {DealId}.", userId, dealId);
                throw new ForbiddenException("You are not authorized to modify this deal.");
            }

            var stateMachine = _state.Create(deal);
            var canModify = stateMachine.CanModify();
            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Cannot remove products from Deal {DealId} due to status: {Status}.", dealId, deal.Status);
                return canModify;
            }

            var dealProduct = deal.DealProducts.FirstOrDefault(dp => dp.Id == dealProductId);
            if (dealProduct == null)
            {
                _logger.LogInformation("DealProduct with ID {DealProductId} does not exist in Deal {DealId}.", dealProductId, dealId);
                return Result.Failure(
                    message: "Product does not exist in the deal.",
                    errorCode: ErrorCodes.ProductNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (deal.DealProducts.Count <= 1)
            {
                _logger.LogWarning("Attempted to remove the last product from Deal {DealId}.", dealId);
                return Result.Failure(
                    message: "Deal must contain at least one product. Cancel the deal instead.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            deal.DealProducts.Remove(dealProduct);
            _context.DealProducts.Remove(dealProduct);

            deal.Value = deal.DealProducts.Sum(dp => (long)dp.Quantity * dp.UnitPrice);

            await _context.SaveChangesAsync();

            _logger.LogInformation("DealProduct {DealProductId} removed from Deal {DealId} by User {UserId}.", dealProductId, dealId, userId);

            return Result.Success(
                message: "Product removed from deal successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result> EditDealProductAsync(Guid dealId, Guid userId, EditDealProductCommand command)
        {
            var deal = await _context.Deals
                .Include(d => d.DealProducts)
                .FirstOrDefaultAsync(d => d.Id == dealId);

            if (deal == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found when attempting to remove product.", dealId);
                return Result.Failure(
                    message: "Deal not found.",
                    errorCode: ErrorCodes.DealNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(userId, deal.OwnerId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized modification of Deal {DealId}.", userId, dealId);
                throw new ForbiddenException("You are not authorized to modify this deal.");
            }

            var stateMachine = _state.Create(deal);
            var canModify = stateMachine.CanModify();
            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Cannot remove products from Deal {DealId} due to status: {Status}.", dealId, deal.Status);
                return canModify;
            }

            var dealProduct = deal.DealProducts.FirstOrDefault(dp => dp.Id == command.DealProductId);
            if (dealProduct == null)
            {
                _logger.LogInformation("DealProduct with ID {DealProductId} does not exist in Deal {DealId}.", command.DealProductId, dealId);
                return Result.Failure(
                    message: "Product does not exist in the deal.",
                    errorCode: ErrorCodes.ProductNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            foreach (var product in deal.DealProducts)
            {
                if (product.Id != command.DealProductId && product.ProductId == dealProduct.ProductId)
                {
                    _logger.LogInformation("Duplicate product with ID {ProductId} found in Deal {DealId} when attempting to edit.", dealProduct.ProductId, dealId);
                    return Result.Failure(
                        message: "Another product with the same ProductId already exists in the deal.",
                        errorCode: ErrorCodes.InvalidOperation,
                        statusCode: StatusCodes.Status400BadRequest
                    );
                }
            }

            var oldValue = deal.Value - (long)dealProduct.Quantity * dealProduct.UnitPrice;
            deal.Value = oldValue;

            if (command.Quantity.HasValue)
            {
                var stockValidation = await _inventory.ValidateStockAvailabilityAsync(
                        dealProduct.ProductId,
                        command.Quantity.Value,
                        dealProduct.Quantity
                    );

                if (!stockValidation.IsSuccess)
                {
                    return stockValidation;
                }
                dealProduct.Quantity = command.Quantity.Value;
            }

            if (command.UnitPrice.HasValue)
            {
                dealProduct.UnitPrice = command.UnitPrice.Value;
            }

            deal.Value = deal.DealProducts.Sum(dp => (long)dp.Quantity * dp.UnitPrice);

            await _context.SaveChangesAsync();

            _logger.LogInformation("DealProduct {DealProductId} updated in Deal {DealId} by User {UserId}.", command.DealProductId, dealId, userId);

            return Result.Success(
                message: "Product updated in deal successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<ChangeDealStatusResponse>> ChangeDealStatusAsync(ChangeDealStatusCommand command)
        {
            var deal = await _context.Deals
                .Include(d => d.Currency)
                .Include(d => d.Contact)
                    .ThenInclude(ct => ct.ContactDetails)
                .Include(d => d.Company)
                    .ThenInclude(c => c.Contacts)
                        .ThenInclude(ct => ct.ContactDetails)
                .Include(d => d.DealProducts)
                    .ThenInclude(dp => dp.Product)
                        .ThenInclude(p => p.Unit)
                .FirstOrDefaultAsync(d => d.Id == command.DealId);

            if (deal == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found when attempting to change status.", command.DealId);
                return Result<ChangeDealStatusResponse>.Failure(
                    message: "Deal not found.",
                    errorCode: ErrorCodes.DealNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (deal.OwnerId != command.UserId)
            {
                _logger.LogWarning("User {UserId} attempted to change status of Deal {DealId} without ownership.", command.UserId, command.DealId);
                return Result<ChangeDealStatusResponse>.Failure(
                    message: "You are not the owner of this deal.",
                    errorCode: ErrorCodes.DealNotOwned,
                    statusCode: StatusCodes.Status403Forbidden
                );
            }

            var stateMachine = _state.Create(deal);
            var transitionResult = stateMachine.TransitionTo(command.TargetStatus);

            if (!transitionResult.IsSuccess)
            {
                _logger.LogWarning("Deal {DealId} cannot transition to status {TargetStatus}.", command.DealId, command.TargetStatus);
                return Result<ChangeDealStatusResponse>.Failure(
                    message: transitionResult.Message ?? "Failed to change deal status.",
                    errorCode: transitionResult.ErrorCode ?? ErrorCodes.InvalidOperation,
                    statusCode: transitionResult.StatusCode
                );
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (command.TargetStatus == DealsStatusEnum.Complete)
                {
                    var inventory = await _inventory.DeductStockForDealAsync(deal.DealProducts);

                    if (!inventory.IsSuccess)
                    {
                        _logger.LogWarning("Failed to deduct stock for Deal {DealId} when changing status to {TargetStatus}.", command.DealId, command.TargetStatus);
                        return Result<ChangeDealStatusResponse>.Failure(
                            message: inventory.Message ?? "Failed to deduct stock for deal.",
                            errorCode: inventory.ErrorCode ?? ErrorCodes.InventoryDeductionFailed,
                            statusCode: inventory.StatusCode
                        );
                    }
                }

                var affectedRows = await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transaction failed for Deal {DealId} when changing status to {TargetStatus}.", command.DealId, command.TargetStatus);
                await transaction.RollbackAsync();
                throw;
            }

            string? recipientEmail = null;

            if (command.TargetStatus == DealsStatusEnum.Complete)
            {
                recipientEmail = ResolveRecipientEmail(deal, command.CustomRecipientEmail);
                await _publisher.Publish(new DealCompletedEvent(deal.Id, recipientEmail, command.Language));
            }

            return Result<ChangeDealStatusResponse>.Success(
                message: $"Deal status changed to {command.TargetStatus}.",
                statusCode: StatusCodes.Status200OK,
                data: new ChangeDealStatusResponse
                {
                    Status = command.TargetStatus.ToString(),
                    SentToEmail = recipientEmail
                }
            );
        }

        private static string? ResolveRecipientEmail(Deal deal, string? customEmail)
        {
            if (!string.IsNullOrWhiteSpace(customEmail))
            {
                return customEmail;
            }

            var dealContactEmail = deal.Contact?.ContactDetails?
                .FirstOrDefault(cd => cd.Type == ContactDetailTypeEnum.EMAIL && cd.IsPrimary)?.Value
                ?? deal.Contact?.ContactDetails?
                .FirstOrDefault(cd => cd.Type == ContactDetailTypeEnum.EMAIL)?.Value;

            if (!string.IsNullOrWhiteSpace(dealContactEmail))
            {
                return dealContactEmail;
            }

            var primaryCompanyEmail = deal.Company?.Contacts?
                .Where(c => c.IsPrimary)
                .SelectMany(c => c.ContactDetails)
                .FirstOrDefault(cd => cd.Type == ContactDetailTypeEnum.EMAIL && cd.IsPrimary)?.Value;

            if (!string.IsNullOrWhiteSpace(primaryCompanyEmail))
            {
                return primaryCompanyEmail;
            }

            return deal.Company?.Contacts?
                .SelectMany(c => c.ContactDetails)
                .FirstOrDefault(cd => cd.Type == ContactDetailTypeEnum.EMAIL)?.Value;
        }

        public async Task<Result> ChangeDealContactAsync(Guid dealId, Guid contactId, Guid currentUserId)
        {
            var deal = await _context.Deals.FirstOrDefaultAsync(d => d.Id == dealId);

            if (deal == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found when attempting to change contact.", dealId);
                return Result.Failure(
                    message: "Deal not found.",
                    errorCode: ErrorCodes.DealNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(currentUserId, deal.OwnerId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized contact modification on Deal {DealId}.", currentUserId, dealId);
                throw new ForbiddenException("You are not authorized to modify this deal.");
            }

            var stateMachine = _state.Create(deal);
            var canModify = stateMachine.CanModify();

            if (!canModify.IsSuccess)
            {
                _logger.LogWarning("Cannot change contact for Deal {DealId} due to status: {Status}.", dealId, deal.Status);
                return canModify;
            }

            if (deal.ContactId == contactId)
            {
                return Result.Success(
                    message: "Contact is already assigned to this deal.",
                    statusCode: StatusCodes.Status200OK
                );
            }

            var contact = await _context.Contacts
                .Include(c => c.ContactDetails)
                .FirstOrDefaultAsync(c => c.Id == contactId);

            if (contact == null)
            {
                _logger.LogInformation("Contact with ID {ContactId} not found when attempting to change contact for Deal {DealId}.", contactId, dealId);
                return Result.Failure(
                    message: "Contact not found.",
                    errorCode: ErrorCodes.ContactNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            if (contact.CompanyId != deal.CompanyId)
            {
                _logger.LogWarning("Contact {ContactId} does not belong to Company {CompanyId} linked to Deal {DealId}.",
                    contact.Id, deal.CompanyId, deal.Id);

                return Result.Failure(
                    message: "Contact does not belong to the company associated with this deal.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            var hasValidEmail = contact.ContactDetails
                .Any(cd => !cd.IsDeleted && cd.Type == ContactDetailTypeEnum.EMAIL && !string.IsNullOrWhiteSpace(cd.Value));

            if (!hasValidEmail)
            {
                _logger.LogWarning("Contact {ContactId} has no active email address when assigned to Deal {DealId}.", contact.Id, deal.Id);
                return Result.Failure(
                    message: "The new contact must have at least one valid email address.",
                    errorCode: ErrorCodes.InvalidOperation,
                    statusCode: StatusCodes.Status400BadRequest
                );
            }

            deal.ContactId = contact.Id;
            deal.UpdateAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Result.Success(
                message: "Deal contact changed successfully.",
                statusCode: StatusCodes.Status200OK
            );
        }

        public async Task<Result<List<DealAssignableContactResponse>>> GetAssignableContactsForDealAsync(Guid dealId, Guid currentUserId)
        {
            var deal = await _context.Deals
                .AsNoTracking()
                .Where(d => d.Id == dealId)
                .Select(d => new
                {
                    d.Id,
                    d.CompanyId,
                    d.ContactId,
                    d.OwnerId
                })
                .FirstOrDefaultAsync();

            if (deal == null)
            {
                _logger.LogInformation("Deal with ID {DealId} not found when attempting to retrieve assignable contacts.", dealId);
                return Result<List<DealAssignableContactResponse>>.Failure(
                    message: "Deal not found.",
                    errorCode: ErrorCodes.DealNotFound,
                    statusCode: StatusCodes.Status404NotFound
                );
            }

            var hasAccess = await _entityAuth.CanModifyAsync(currentUserId, deal.OwnerId);
            if (!hasAccess)
            {
                _logger.LogWarning("User {UserId} attempted unauthorized access to assignable contacts for Deal {DealId}.", currentUserId, dealId);
                throw new ForbiddenException("You are not authorized to view assignable contacts for this deal.");
            }

            var contacts = await _context.Contacts
                .AsNoTracking()
                .Where(c => c.CompanyId == deal.CompanyId
                         && c.Id != deal.ContactId
                         && c.ContactDetails.Any(cd => !cd.IsDeleted && cd.Type == ContactDetailTypeEnum.EMAIL && !string.IsNullOrWhiteSpace(cd.Value)))
                .OrderByDescending(c => c.IsPrimary)
                .ThenBy(c => c.FirstName)
                .ThenBy(c => c.LastName)
                .Select(c => new DealAssignableContactResponse
                {
                    Id = c.Id,
                    FullName = c.FirstName + " " + c.LastName,
                    JobTitle = c.JobTitle,
                    IsPrimary = c.IsPrimary,
                    Email = c.ContactDetails
                        .Where(cd => !cd.IsDeleted && cd.Type == ContactDetailTypeEnum.EMAIL && !string.IsNullOrWhiteSpace(cd.Value))
                        .OrderByDescending(cd => cd.IsPrimary)
                        .Select(cd => cd.Value)
                        .First()
                })
                .ToListAsync();

            return Result<List<DealAssignableContactResponse>>.Success(
                message: "Assignable contacts retrieved successfully.",
                statusCode: StatusCodes.Status200OK,
                data: contacts
            );
        }

        public async Task<Result<PagedResult<ProductDealItemResponse>>> GetProductDealsAsync(Guid productId, SimpleListCommand command) 
            => await _context.DealProducts
                .Where(dp => dp.ProductId == productId)
                .AsNoTracking()
                .ApplyProductDealSearch(command.SearchTerm)
                .OrderByDescending(dp => dp.Deal.CreatedAt)
                .Select(dp => new ProductDealItemResponse
                {
                    DealId = dp.DealId,
                    DealName = dp.Deal.Name,
                    CompanyName = dp.Deal.Company.Name,
                    Status = dp.Deal.Status.ToString(),
                    Quantity = dp.Quantity,
                    UnitPrice = dp.UnitPrice,
                    TotalPrice = (long)dp.Quantity * dp.UnitPrice,
                    CurrencyCode = dp.Deal.Currency.Code,
                    DecimalPlaces = dp.Deal.Currency.DecimalPlaces,
                    CloseDate = dp.Deal.CloseDate
                }).ToPagedResultAsync(command.PageNumber, command.PageSize, _logger, "product_deals");

    }
}
