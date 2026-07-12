using Domain.Enum;
using Domain.Models;
using DTO.Request;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Services.Services;
using Testcontainers.PostgreSql;

namespace Tests.Services
{
    public class SalesServicesTest
    {
        protected AppDbContext _contextMock = null!;

        private static PostgreSqlContainer _dbContainer = null!;
        private static string _connectionString = null!;

        protected SalesServices _salesServicesMock = null!;
        protected ILogger<SalesServices> _loggerMock = null!;

        [Before(Class)]
        [Obsolete]
        public static async Task SetupClassAsync()
        {
            _dbContainer = new PostgreSqlBuilder()
                .WithImage("postgis/postgis:18-3.6")
                .WithDatabase("testdb")
                .WithUsername("testuser")
                .WithPassword("testpassword")
                .Build();

            await _dbContainer.StartAsync();

            _connectionString = _dbContainer.GetConnectionString();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite();
                })
                .Options;
            using var context = new AppDbContext(dbOptions);
            await context.Database.EnsureCreatedAsync();

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
        }

        [After(Class)]
        public static async Task CleanupClassAsync()
            => await _dbContainer.DisposeAsync();

        [Before(Test)]
        public async Task SetupAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(_connectionString, options =>
                {
                    options.UseNetTopologySuite();
                })
                .Options;

            _contextMock = new AppDbContext(dbOptions);
            await _contextMock.Database.EnsureCreatedAsync();

            _loggerMock = new LoggerFactory().CreateLogger<SalesServices>();

            _salesServicesMock = new SalesServices(_contextMock, _loggerMock);
        }

        [After(Test)]
        public async Task CleanupAsync()
        {
            if (_contextMock != null)
            {
                await _contextMock.DisposeAsync();
            }
        }

        // ─── GetUserSales ─────────────────────────────────────────────────

        [Test]
        public async Task GetUserSales_FiltersByOwnerAndMapsPropertiesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var targetUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            var targetUser = new ApplicationUser
            {
                Id = targetUserId,
                UserName =
                $"Target_{uniqueSuffix}",
                NormalizedUserName = $"TARGET_{uniqueSuffix}",
                Email = $"t_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"T_{uniqueSuffix}@T.PL",
                FirstName = "Target",
                LastName = "User"
            };

            var otherUser = new ApplicationUser
            {
                Id = otherUserId,
                UserName = $"Other_{uniqueSuffix}",
                NormalizedUserName = $"OTHER_{uniqueSuffix}",
                Email = $"o_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"O_{uniqueSuffix}@T.PL",
                FirstName = "Other",
                LastName = "User"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Company_{uniqueSuffix}",
                NIP = "1234567890",
                OwnerId = targetUserId,
                Owner = targetUser
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Target Deal",
                Value = 1500000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = targetUserId,
                Owner = targetUser,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Other Deal",
                Value = 3000000, // 300.00
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = otherUserId,
                Owner = otherUser,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.AddRange(targetUser, otherUser);
            _contextMock.Companies.Add(company);
            _contextMock.Currencies.Add(currency);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            await _contextMock.SaveChangesAsync();

            var pagged = new PaggedRequest
            {
                PageNumber = 1,
                PageSize = 10
            };

            var filter = new SalesFilterRequest();
            var search = new SearchRequest();
            var sorting = new SortingRequest();

            // Act
            var result = await _salesServicesMock.GetUserSales(targetUserId, pagged, sorting, search, filter);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).HasCount().EqualTo(1);

            var mappedDeal = items.First();

            await Assert.That(mappedDeal.Id).IsEqualTo(targetDeal.Id);
            await Assert.That(mappedDeal.Value).IsEqualTo(150.00m);
            await Assert.That(mappedDeal.Currency).IsEqualTo("PLN");
            await Assert.That(mappedDeal.DecimalPlace).IsEqualTo(2);
            await Assert.That(mappedDeal.CompanyName).IsEqualTo(company.Name);
            await Assert.That(mappedDeal.Nip).IsEqualTo(company.NIP);
            await Assert.That(mappedDeal.Status).IsEqualTo(targetDeal.Status.ToString());
        }

        [Test]
        public async Task GetUserSales_WhenUserHasNoSales_ReturnsEmptyListWithSuccessStatus()
        {
            var randomUserId = Guid.NewGuid();

            var pagged = new PaggedRequest { PageNumber = 1, PageSize = 10 };
            var filter = new SalesFilterRequest();
            var search = new SearchRequest();
            var sorting = new SortingRequest();

            var result = await _salesServicesMock.GetUserSales(randomUserId, pagged, sorting, search, filter);

            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
        }

        // ─── GetSalesStatus ─────────────────────────────────────────────────

        [Test]
        public async Task GetSalesStatus_ReturnsAllEnumValues()
        {
            // Arrange
            var expectedStatuses = Enum.GetNames(typeof(DealsStatusEnum)).ToList();

            // Act
            var result = await _salesServicesMock.GetSalesStatus();

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!;

            await Assert.That(items).HasCount().EqualTo(expectedStatuses.Count);

            foreach (var status in expectedStatuses)
            {
                await Assert.That(items.Contains(status)).IsTrue();
            }
        }

        // ─── GetComapanySalesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetComapanySalesAsync_FiltersByCompanyAndMapsPropertiesCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var salesmanId = Guid.NewGuid();

            var salesman = new ApplicationUser
            {
                Id = salesmanId,
                UserName = $"User_{uniqueSuffix}",
                NormalizedUserName = $"USER_{uniqueSuffix}",
                Email = $"u_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"U_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var targetCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Target_{uniqueSuffix}",
                NIP = "111",
                OwnerId = salesmanId,
                Owner = salesman
            };

            var otherCompany = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Other_{uniqueSuffix}",
                NIP = "222",
                OwnerId = salesmanId,
                Owner = salesman
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt X",
                Value = 5005000,
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow,
                CompanyId = targetCompany.Id,
                Company = targetCompany,
                OwnerId = salesmanId,
                Owner = salesman,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Projekt Y",
                Value = 1000000,
                Status = DealsStatusEnum.InProgress,
                CloseDate = DateTime.UtcNow,
                CompanyId = otherCompany.Id,
                Company = otherCompany,
                OwnerId = salesmanId,
                Owner = salesman,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.Add(salesman);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.AddRange(targetCompany, otherCompany);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            await _contextMock.SaveChangesAsync();

            var pagged = new PaggedRequest
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act
            var result = await _salesServicesMock.GetComapanySalesAsync(targetCompany.Id, pagged);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).HasCount().EqualTo(1);

            var mappedDeal = items.First();

            await Assert.That(mappedDeal.Id).IsEqualTo(targetDeal.Id);
            await Assert.That(mappedDeal.Name).IsEqualTo("Projekt X");
            await Assert.That(mappedDeal.Value).IsEqualTo(500.50m);
            await Assert.That(mappedDeal.SalesmanFirstName).IsEqualTo("Tomasz");
            await Assert.That(mappedDeal.SalesmanLastName).IsEqualTo("Kowalski");
            await Assert.That(mappedDeal.Code).IsEqualTo("EUR");
            await Assert.That(mappedDeal.DecimalPlaces).IsEqualTo(2);
        }

        [Test]
        public async Task GetComapanySalesAsync_AppliesPaginationCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "123",
                OwnerId = userId,
                Owner = user
            };

            var deals = new List<Deal>
            {
                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D1",
                    Value = 10000,
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = user,
                    CurrencyId = currency.Id,
                    Currency = currency,
                    CloseDate = DateTime.UtcNow
                },

                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D2",
                    Value = 20000,
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = user,
                    CurrencyId = currency.Id,
                    Currency = currency,
                    CloseDate = DateTime.UtcNow
                },

                new Deal
                {
                    Id = Guid.NewGuid(),
                    Name = "D3",
                    Value = 30000,
                    CompanyId = company.Id,
                    Company = company,
                    OwnerId = userId,
                    Owner = user,
                    CurrencyId = currency.Id,
                    Currency = currency,
                    CloseDate = DateTime.UtcNow
                }
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.AddRange(deals);
            await _contextMock.SaveChangesAsync();

            var pagged = new PaggedRequest
            {
                PageNumber = 1,
                PageSize = 2
            };

            // Act
            var result = await _salesServicesMock.GetComapanySalesAsync(company.Id, pagged);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();
        }

        [Test]
        public async Task GetComapanySalesAsync_WhenNoSalesFound_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var randomCompanyId = Guid.NewGuid();
            var pagged = new PaggedRequest { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await _salesServicesMock.GetComapanySalesAsync(randomCompanyId, pagged);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();

            await Assert.That(result.Data!.Items).IsEmpty();
        }

        // ─── GetSaleDetailAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetSaleDetailAsync_WhenDealDoesNotExist_Returns404NotFound()
        {
            // Arrange
            var randomDealId = Guid.NewGuid();

            // Act
            var result = await _salesServicesMock.GetSaleDetailAsync(randomDealId);

            // Assert
            await Assert.That(result.IsSuccess).IsFalse();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
            await Assert.That(result.Message).IsEqualTo("Sale not found");
        }

        [Test]
        public async Task GetSaleDetailAsync_WhenDealHasNoInvoices_ReturnsAggregatesAsZero()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2,
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "New Project",
                Value = 1000000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _salesServicesMock.GetSaleDetailAsync(deal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;

            await Assert.That(data.Value).IsEqualTo(1000000);
            await Assert.That(data.InvoicedAmount).IsEqualTo(0);
            await Assert.That(data.PaidAmount).IsEqualTo(0);
            await Assert.That(data.IsOverduelInvoices).IsFalse();
            await Assert.That(data.PaymentPercentage).IsEqualTo(0);
        }

        [Test]
        public async Task GetSaleDetailAsync_CalculatesAggregatesAndOverdueStatusCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U2_{uniqueSuffix}",
                NormalizedUserName = $"U2_{uniqueSuffix}",
                Email = $"e2_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E2_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp2_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Complex Project",
                Value = 1000000,
                Status = DealsStatusEnum.Complete,
                CloseDate = DateTime.UtcNow,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var invoice1 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/1",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 400000,
                PaidAmount = 400000,
                DueDate = DateTime.UtcNow.AddDays(-10)
            };

            var invoice2 = new Invoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = "F/2",
                CompanyId = company.Id,
                Company = company,
                CurrencyId = currency.Id,
                Currency = currency,
                TotalAmount = 600000,
                PaidAmount = 150000,
                DueDate = DateTime.UtcNow.AddDays(-1)
            };

            deal.Invoices = new List<Invoice> { invoice1, invoice2 };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _salesServicesMock.GetSaleDetailAsync(deal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var data = result.Data!;

            await Assert.That(data.CompanyName).IsEqualTo(company.Name);
            await Assert.That(data.CurrencyCode).IsEqualTo("EUR");
            await Assert.That(data.InvoicedAmount).IsEqualTo(1000000);
            await Assert.That(data.PaidAmount).IsEqualTo(550000);
            await Assert.That(data.IsOverduelInvoices).IsTrue();
            await Assert.That(data.PaymentPercentage).IsEqualTo(55);
        }

        // ─── GetDealProductAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetDealProductAsync_MapsDeepRelationsAndCalculatesTotalsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Złoty",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = user
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Deal",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency,
                CloseDate = DateTime.UtcNow
            };

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Sztuka",
                Symbol = "szt"
            };

            var category = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Cat_{uniqueSuffix}",
                Description = "Opis kategorii"
            };

            var type = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Profil",
                CategoryId = category.Id,
                Category = category,
                Description = "Dwdadadasd"
            };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Produkt A",
                SteelGrade = "S235",
                Thickness = 2,
                Width = 40,
                Length = 6000,
                PricePerUnit = 10000,
                UnitId = unit.Id,
                Unit = unit,
                ProductTypeId = type.Id,
                ProductType = type
            };

            var dealProduct = new DealProduct
            {
                Id = Guid.NewGuid(),
                DealId = deal.Id,
                Deal = deal,
                ProductId = product.Id,
                Product = product,
                Quantity = 5,
                UnitPrice = 12000
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.ProductCategories.Add(category);
            _contextMock.ProductTypes.Add(type);
            _contextMock.Products.Add(product);
            _contextMock.DealProducts.Add(dealProduct);
            await _contextMock.SaveChangesAsync();

            var pagged = new PaggedRequest { PageNumber = 1, PageSize = 10 };
            var sorting = new SortingRequest();
            var search = new SearchRequest();
            var filter = new ProductFilterRequest();

            // Act
            var result = await _salesServicesMock.GetDealProductAsync(deal.Id, pagged, sorting, search, filter);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;
            await Assert.That(items).HasCount().EqualTo(1);

            var mapped = items.First();

            await Assert.That(mapped.Name).IsEqualTo("Produkt A");
            await Assert.That(mapped.SteelGrade).IsEqualTo("S235");
            await Assert.That(mapped.UnitSymbol).IsEqualTo("szt");
            await Assert.That(mapped.CurrencyCode).IsEqualTo("PLN");
            await Assert.That(mapped.DecimalPlaces).IsEqualTo(2);
            await Assert.That(mapped.Quantity).IsEqualTo(5);
            await Assert.That(mapped.BaseUnitPrice).IsEqualTo(10000);
            await Assert.That(mapped.UnitPrice).IsEqualTo(12000);
            await Assert.That(mapped.TotalPrice).IsEqualTo(60000);
            await Assert.That(mapped.Dimensions).IsNotNull();
            await Assert.That(mapped.Dimensions).IsNotEmpty();
        }

        [Test]
        public async Task GetDealProductAsync_ReturnsProductsOnlyForSpecificDeal()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"U2_{uniqueSuffix}",
                NormalizedUserName = $"U2_{uniqueSuffix}",
                Email = $"e2_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E2_{uniqueSuffix}@T.PL",
                FirstName = "Tomasz",
                LastName = "Kowalski"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "Euro",
                Code = "EUR",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"C2_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = user
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Target",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency,
                CloseDate = DateTime.UtcNow
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Other",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = user,
                CurrencyId = currency.Id,
                Currency = currency,
                CloseDate = DateTime.UtcNow
            };

            var unit = new UnitOfMeasure
            {
                Id = Guid.NewGuid(),
                Name = "Metr",
                Symbol = "m"
            };

            var category = new ProductCategory
            {
                Id = Guid.NewGuid(),
                Name = $"Kat2_{uniqueSuffix}",
                Description = "Opis kategorii"
            };

            var type = new ProductType
            {
                Id = Guid.NewGuid(),
                Name = "Rura",
                CategoryId = category.Id,
                Category = category,
                Description = "Opis typu produktu"
            };

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Prod B",
                SteelGrade = "S355",
                UnitId = unit.Id,
                Unit = unit,
                ProductTypeId = type.Id,
                ProductType = type
            };

            var targetDealProduct = new DealProduct
            {
                Id = Guid.NewGuid(),
                DealId = targetDeal.Id,
                Deal = targetDeal,
                ProductId = product.Id,
                Product = product,
                Quantity = 1
            };

            var otherDealProduct = new DealProduct
            {
                Id = Guid.NewGuid(),
                DealId = otherDeal.Id,
                Deal = otherDeal,
                ProductId = product.Id,
                Product = product,
                Quantity = 2
            };

            _contextMock.Users.Add(user);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            _contextMock.UnitsOfMeasure.Add(unit);
            _contextMock.ProductCategories.Add(category);
            _contextMock.ProductTypes.Add(type);
            _contextMock.Products.Add(product);
            _contextMock.DealProducts.AddRange(targetDealProduct, otherDealProduct);
            await _contextMock.SaveChangesAsync();

            var pagged = new PaggedRequest
            {
                PageNumber = 1,
                PageSize = 10
            };

            // Act 
            var result = await _salesServicesMock.GetDealProductAsync(targetDeal.Id, pagged, new SortingRequest(), new SearchRequest(), new ProductFilterRequest());

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!.Items;

            await Assert.That(items).HasCount().EqualTo(1);
            await Assert.That(items.First().Quantity).IsEqualTo(1);
        }

        [Test]
        public async Task GetDealProductAsync_WhenDealHasNoProducts_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var randomDealId = Guid.NewGuid();

            var pagged = new PaggedRequest { PageNumber = 1, PageSize = 10 };
            var sorting = new SortingRequest();
            var search = new SearchRequest();
            var filter = new ProductFilterRequest();

            // Act
            var result = await _salesServicesMock.GetDealProductAsync(randomDealId, pagged, sorting, search, filter);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);
            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!.Items).IsEmpty();
        }

        // ─── GetDealNotesAsync ─────────────────────────────────────────────────

        [Test]
        public async Task GetDealNotesAsync_FiltersDeletedAndOtherTypes_AndMapsCorrectly()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var author = new ApplicationUser
            {
                Id = userId,
                UserName = $"U_{uniqueSuffix}",
                NormalizedUserName = $"U_{uniqueSuffix}",
                Email = $"e_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E_{uniqueSuffix}@T.PL",
                FirstName = "Anna",
                LastName = "Nowak"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"Comp_{uniqueSuffix}",
                NIP = "111",
                OwnerId = userId,
                Owner = author
            };

            var targetDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Target Deal",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var otherDeal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Other Deal",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                FirstName = "Jan",
                LastName = "Kowalski",
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                IsPrimary = true,
            };

            var validNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Valid Note",
                Content = "Treść",
                DealId = targetDeal.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            var deletedNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Deleted Note",
                Content = "Treść",
                DealId = targetDeal.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = true,
                CreatedAt = DateTime.UtcNow
            };

            var otherDealNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Other Deal Note",
                Content = "Treść",
                DealId = otherDeal.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            var contactNote = new ContactNote
            {
                Id = Guid.NewGuid(),
                Title = "Contact Note",
                Content = "Treść",
                ContactId = contact.Id,
                AuthorId = userId,
                Author = author,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            _contextMock.Users.Add(author);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.AddRange(targetDeal, otherDeal);
            _contextMock.Contacts.Add(contact);
            _contextMock.Notes.AddRange(validNote, deletedNote, otherDealNote, contactNote);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _salesServicesMock.GetDealNotesAsync(targetDeal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!;

            await Assert.That(items).HasCount().EqualTo(1);

            var mappedNote = items.First();
            await Assert.That(mappedNote.NoteId).IsEqualTo(validNote.Id);
            await Assert.That(mappedNote.Title).IsEqualTo("Valid Note");
            await Assert.That(mappedNote.AuthorFirstName).IsEqualTo("Anna");
            await Assert.That(mappedNote.AuthorLastName).IsEqualTo("Nowak");
        }

        [Test]
        public async Task GetDealNotesAsync_SortsNotesByCreatedAtDescending()
        {
            // Arrange
            var uniqueSuffix = Guid.NewGuid().ToString("N");
            var userId = Guid.NewGuid();

            var author = new ApplicationUser
            {
                Id = userId,
                UserName = $"U2_{uniqueSuffix}",
                NormalizedUserName = $"U2_{uniqueSuffix}",
                Email = $"e2_{uniqueSuffix}@t.pl",
                NormalizedEmail = $"E2_{uniqueSuffix}@T.PL",
                FirstName = "Anna",
                LastName = "Nowak"
            };

            var currency = new Currency
            {
                Id = Guid.NewGuid(),
                Name = "PLN",
                Code = "PLN",
                DecimalPlaces = 2
            };

            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = $"C2_{uniqueSuffix}",
                NIP = "222",
                OwnerId = userId,
                Owner = author
            };

            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                Name = "Deal",
                Value = 0,
                CompanyId = company.Id,
                Company = company,
                OwnerId = userId,
                Owner = author,
                CurrencyId = currency.Id,
                Currency = currency
            };

            var now = DateTime.UtcNow;

            var oldNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Old",
                Content = "...",
                DealId = deal.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now.AddDays(-5)
            };

            var newestNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Newest",
                Content = "...",
                DealId = deal.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now
            };

            var middleNote = new DealNote
            {
                Id = Guid.NewGuid(),
                Title = "Middle",
                Content = "...",
                DealId = deal.Id,
                AuthorId = userId,
                Author = author,
                CreatedAt = now.AddDays(-2)
            };

            _contextMock.Users.Add(author);
            _contextMock.Currencies.Add(currency);
            _contextMock.Companies.Add(company);
            _contextMock.Deals.Add(deal);
            _contextMock.Notes.AddRange(oldNote, middleNote, newestNote);
            await _contextMock.SaveChangesAsync();

            // Act
            var result = await _salesServicesMock.GetDealNotesAsync(deal.Id);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.Data).IsNotNull();

            var items = result.Data!;

            await Assert.That(items).HasCount().EqualTo(3);
            await Assert.That(items[0].NoteId).IsEqualTo(newestNote.Id);
            await Assert.That(items[1].NoteId).IsEqualTo(middleNote.Id);
            await Assert.That(items[2].NoteId).IsEqualTo(oldNote.Id);
        }

        [Test]
        public async Task GetDealNotesAsync_WhenNoNotesExist_ReturnsEmptyListWithSuccessStatus()
        {
            // Arrange
            var randomDealId = Guid.NewGuid();

            // Act
            var result = await _salesServicesMock.GetDealNotesAsync(randomDealId);

            // Assert
            await Assert.That(result.IsSuccess).IsTrue();
            await Assert.That(result.StatusCode).IsEqualTo(StatusCodes.Status200OK);

            await Assert.That(result.Data).IsNotNull();
            await Assert.That(result.Data!).IsEmpty();
        }
    }
}
