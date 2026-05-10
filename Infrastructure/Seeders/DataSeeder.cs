using Domain;
using Domain.Enum;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Infrastructure.Seeders
{
    public class DataSeeder
    {
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private AppDbContext _context;

        public DataSeeder(
            RoleManager<IdentityRole<Guid>> roleManager,
            UserManager<ApplicationUser> userManager,
            AppDbContext context
        )
        {
            _roleManager = roleManager;
            _userManager = userManager;
            _context = context;
        }

        public async Task InitAsync()
        {
            if (!await _roleManager.Roles.AnyAsync()) await SeedRoleAsync();
            if (!await _userManager.Users.AnyAsync()) await SeedUserAsync();

            if (!await _context.Currencies.AnyAsync()) await SeedCurrenciesAsync();
            if (!await _context.UnitsOfMeasure.AnyAsync()) await SeedUnitsAsync();
            if (!await _context.ProductCategories.AnyAsync()) await SeedProductCatalogAsync();

            if (!await _context.Companies.AnyAsync()) await SeedCompaniesAndContactsAsync();
            if (!await _context.Products.AnyAsync()) await SeedProductsAsync();

            if (!await _context.Deals.AnyAsync()) await SeedDealsAndTasksAsync();
        }

        private async Task SeedRoleAsync()
        {
            try
            {
                string[] roleNames = { "Admin", "Manager", "User" };

                foreach (var roleName in roleNames)
                {
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        await _roleManager.CreateAsync(new IdentityRole<Guid>
                        {
                            Name = roleName,
                            NormalizedName = roleName.ToUpper()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while seeding roles: {ex.Message} | {ex.InnerException}");
            }
        }


        private async Task SeedUserAsync()
        {
            try
            {
                if (!await _roleManager.RoleExistsAsync("Admin") ||
                    !await _roleManager.RoleExistsAsync("Manager") ||
                    !await _roleManager.RoleExistsAsync("User")
                    )
                {
                    await SeedRoleAsync();
                }

                await CreateUserAsync("Admin", "admin@example.pl", "Admin123!", "Admin", "Adam", "Kowalki");
                await CreateUserAsync("User", "user@example.pl", "User123!", "User", "Cezary", "Kowalki");
                await CreateUserAsync("Manager", "manager@example.pl", "Manager123!", "Manager", "Antoni", "Kowalki");

                Console.WriteLine("All users seeded successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while seeding users: {ex.Message} | {ex.InnerException}");
            }
        }

        private async Task CreateUserAsync(
            string userName,
            string email,
            string password,
            string role,
            string firstName,
            string lastName
            )
        {
            var isUserExist = await _userManager.FindByEmailAsync(email);

            if (isUserExist == null)
            {
                ApplicationUser newUser = new ApplicationUser
                {
                    Email = email,
                    NormalizedEmail = email.ToUpper(),
                    UserName = userName,
                    NormalizedUserName = userName.ToUpper(),
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true,
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    SecurityStamp = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                var result = await _userManager.CreateAsync(newUser, password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(newUser, role);
                }
                else
                {
                    throw new Exception($"Failed to create {userName}");
                }
            }
        }


        private async Task SeedCurrenciesAsync()
        {
            var currencies = new List<Currency>
            {
                new() { Name = "US Dollar", Code = "USD", DecimalPlaces = 2 },
                new() { Name = "Euro", Code = "EUR", DecimalPlaces = 2 },
                new() { Name = "Polski złoty", Code = "PLN", DecimalPlaces = 2 },
            };

            await _context.Currencies.AddRangeAsync(currencies);
            await _context.SaveChangesAsync();
        }

        private async Task SeedUnitsAsync()
        {
            var units = new List<UnitOfMeasure>
            {
                new() { Name = "Tona", Symbol = "t", BaseMultiplier = 1 },
                new() { Name = "Kilogram", Symbol = "kg", BaseMultiplier = 1 },
                new() { Name = "Sztuka", Symbol = "szt", BaseMultiplier = 1 },
                new() { Name = "Metr bieżący", Symbol = "mb", BaseMultiplier = 1 }
            };

            await _context.UnitsOfMeasure.AddRangeAsync(units);
            await _context.SaveChangesAsync();

        }

        private async Task SeedProductCatalogAsync()
        {
            var catPlaskie = new ProductCategory { Name = "Wyroby Płaskie", Description = "Blachy zimno i gorącowalcowane" };
            var catDlugie = new ProductCategory { Name = "Wyroby Długie", Description = "Pręty, profile, kątowniki" };
            var catRury = new ProductCategory { Name = "Rury", Description = "Rury stalowe bez szwu i spawane" };

            await _context.ProductCategories.AddRangeAsync(catPlaskie, catDlugie, catRury);

            var types = new List<ProductType>
            {
                new() { Name = "Blacha Zimnowalcowana", Description = "Podstawowa blacha zimnowalcowana", Category = catPlaskie },
                new() { Name = "Blacha Ocynkowana", Description = "Blacha pokryta warstwą cynku", Category = catPlaskie },
                new() { Name = "Pręt Żebrowany", Description = "Pręt zbrojeniowy", Category = catDlugie },
                new() { Name = "Profil Zamknięty", Description = "Stalowy profil konstrukcyjny", Category = catDlugie },
                new() { Name = "Rura Stalowa Bezszwowa", Description = "Rura do zastosowań ciśnieniowych", Category = catRury },
                new() { Name = "Rura Stalowa Spawana", Description = "Standardowa rura spawana", Category = catRury }
            };

            await _context.ProductTypes.AddRangeAsync(types);
            await _context.SaveChangesAsync();
        }

        private async Task SeedCompaniesAndContactsAsync()
        {
            var user = await _userManager.FindByEmailAsync("user@example.pl");
            var manager = await _userManager.FindByEmailAsync("manager@example.pl");

            var companies = new List<Company>
            {
                new() {
                    Name = "Stal-Met Sp. z o.o.",
                    NIP = "1234567890",
                    Owner = user,
                    CompanyAdresses = new List<CompanyAdress> {
                        new() {
                            Street = "Przemysłowa 10",
                            City = "Katowice",
                            ZipCode = "40-001",
                            AddressType = AddressTypeEnum.Headquarters,
                            Location = GenerateRandomPoint()
                        }
                    }
                },
                new() {
                    Name = "BudowaX S.A.",
                    NIP = "9876543210",
                    Owner = manager,
                    CompanyAdresses = new List<CompanyAdress> {
                        new() {
                            Street = "Budowlanych 5",
                            City = "Wrocław",
                            ZipCode = "50-002",
                            AddressType = AddressTypeEnum.Branch,
                            Location = GenerateRandomPoint()
                        }
                    }
                },
                new() {
                    Name = "Huta Żelaza 'Odra' S.A.",
                    NIP = "1112223334",
                    Owner = user,
                    CompanyAdresses = new List<CompanyAdress> {
                        new() {
                            Street = "Hutnicza 1",
                            City = "Szczecin",
                            ZipCode = "70-001",
                            AddressType = AddressTypeEnum.Headquarters,
                            Location = GenerateRandomPoint()
                        },
                        new() {
                            Street = "Magazynowa 4",
                            City = "Szczecin",
                            ZipCode = "70-005",
                            AddressType = AddressTypeEnum.Shipping,
                            Location = GenerateRandomPoint()
                        }
                    }
                },
                new() {
                    Name = "P.H.U. Konstrukcje Stalowe",
                    NIP = "5556667778",
                    Owner = manager,
                    CompanyAdresses = new List<CompanyAdress> {
                        new() {
                            Street = "Polna 12",
                            City = "Rzeszów",
                            ZipCode = "35-001",
                            AddressType = AddressTypeEnum.Headquarters,
                            Location = GenerateRandomPoint()
                        }
                    }
                },
                new() {
                    Name = "Mega-Stal s.c.",
                    NIP = "9998887776",
                    Owner = user,
                    CompanyAdresses = new List<CompanyAdress> {
                        new() {
                            Street = "Główna 45",
                            City = "Poznań",
                            ZipCode = "60-001",
                            AddressType = AddressTypeEnum.Headquarters,
                            Location = GenerateRandomPoint()
                        }
                    }
                }
            };

            await _context.Companies.AddRangeAsync(companies);
            await _context.SaveChangesAsync();

            var contacts = new List<Contact>
            {
                new()
                {
                    FirstName = "Andrzej",
                    LastName = "Nowak",
                    Company = companies[0],
                    Owner = user,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = "Email", Value = "a.nowak@stalmet.pl", IsPrimary = true },
                        new() { Type = "Phone", Value = "+48 111 222 333", IsPrimary = false }
                    }
                },
                new()
                {
                    FirstName = "Katarzyna",
                    LastName = "Kowal",
                    Company = companies[2],
                    Owner = manager,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = "Email", Value = "k.kowal@hutaodra.pl", IsPrimary = true }
                    }
                }
            };

            await _context.Contacts.AddRangeAsync(contacts);
            await _context.SaveChangesAsync();
        }

        private async Task SeedProductsAsync()
        {
            var typeBlacha = await _context.ProductTypes.FirstAsync(t => t.Name == "Blacha Zimnowalcowana");
            var typeRura = await _context.ProductTypes.FirstAsync(t => t.Name == "Rura Stalowa Bezszwowa");
            var unitTona = await _context.UnitsOfMeasure.FirstAsync(u => u.Symbol == "t");
            var unitSzt = await _context.UnitsOfMeasure.FirstAsync(u => u.Symbol == "szt");

            var products = new List<Product>
            {
                new() {
                    Name = "Blacha DC01 1.0x1000x2000",
                    SteelGrade = "DC01",
                    Thickness = 100,
                    Width = 1000,
                    Length = 2000,
                    Weight = 16,
                    Unit = unitTona,
                    PricePerUnit = 38000000,
                    StockQuantity = 50,
                    ProductType = typeBlacha
                },
                new() {
                    Name = "Blacha DC01 2.0x1250x2500",
                    SteelGrade = "DC01",
                    Thickness = 200,
                    Width = 1250,
                    Length = 2500,
                    Weight = 50,
                    Unit = unitTona,
                    PricePerUnit = 37500000,
                    StockQuantity = 30,
                    ProductType = typeBlacha
                },
                new()
                {
                    Name = "Rura 108x4.0 S235JR",
                    SteelGrade = "S235JR",
                    Diameter = 108,
                    Thickness = 400,
                    Length = 6000,
                    Width = 0,
                    Weight = 61,
                    Unit = unitSzt,
                    PricePerUnit = 2500000,
                    StockQuantity = 100,
                    ProductType = typeRura
                },
                new()
                {
                    Name = "Rura 219.1x6.3 S355J2",
                    SteelGrade = "S355J2",
                    Diameter = 219,
                    Thickness = 630,
                    Length = 12000,
                    Width = 0,
                    Weight = 396,
                    Unit = unitTona,
                    PricePerUnit = 52000000,
                    StockQuantity = 15,
                    ProductType = typeRura
                }
            };

            await _context.Products.AddRangeAsync(products);
            await _context.SaveChangesAsync();
        }

        private async Task SeedDealsAndTasksAsync()
        {
            var admin = await _userManager.FindByEmailAsync("admin@example.pl");
            var company = await _context.Companies.FirstAsync();
            var currency = await _context.Currencies.FirstAsync(c => c.Code == "PLN");

            var deal = new Deal
            {
                Name = "Negocjacje - Dostawa Jesień",
                Value = 1500000000, // 150,000.0000
                Status = DealsStatusEnum.ToDo,
                CloseDate = DateTime.UtcNow.AddMonths(1),
                Currency = currency,
                Company = company,
                Owner = admin
            };

            await _context.Deals.AddAsync(deal);

            var task = new Domain.Tasks
            {
                Title = "Telefon ofertowy",
                Description = "Zadzwonić i potwierdzić dostępność stanów magazynowych",
                DueAt = DateTime.UtcNow.AddDays(2),
                AssignedTo = admin,
                Deal = deal,
                Status = TaskStatusEnum.ToDo,
                Priority = TaskPriorityEnum.Medium
            };

            await _context.Tasks.AddAsync(task);
            await _context.SaveChangesAsync();
        }

        private Point GenerateRandomPoint()
        {
            var random = new Random();

            double minLng = 14.1;
            double maxLng = 24.1;
            double minLat = 49.0;
            double maxLat = 54.8;

            double lng = minLng + (random.NextDouble() * (maxLng - minLng));
            double lat = minLat + (random.NextDouble() * (maxLat - minLat));

            return new Point(lng, lat) { SRID = 4326 };
        }
    }
}
