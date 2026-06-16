using Domain.Enum;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Infrastructure.Seeders
{
    public class DataSeeder
    {
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;

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

                        Console.WriteLine($"Role '{roleName}' created successfully.");
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
                    Console.WriteLine($"User '{userName}' created successfully with role '{role}'.");
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
            Console.WriteLine("All currencies seeded successfully.");
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
            Console.WriteLine("All units of measure seeded successfully.");
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
            Console.WriteLine("Product categories and types seeded successfully.");
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
            Console.WriteLine("All companies seeded successfully.");

            var contacts = new List<Contact>
            {
                new()
                {
                    FirstName = "Andrzej",
                    LastName = "Nowak",
                    IsPrimary = true,
                    JobTitle = "Dyrektor ds. Zakupów",
                    Company = companies[0], // Stal-Met
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "a.nowak@stalmet.pl", IsPrimary = true },
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 111 222 333", IsPrimary = false }
                    }
                },
                new()
                {
                    FirstName = "Anna",
                    LastName = "Wiśniewska",
                    IsPrimary = false,
                    JobTitle = "Główna Księgowa",
                    Company = companies[0], // Stal-Met
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "a.wisniewska@stalmet.pl", IsPrimary = true },
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 555 666 777", IsPrimary = false }
                    }
                },
                new()
                {
                    FirstName = "Marcin",
                    LastName = "Zieliński",
                    IsPrimary = false,
                    JobTitle = "Kierownik Magazynu",
                    Company = companies[0], // Stal-Met
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 777 888 999", IsPrimary = true }
                    }
                },
                new()
                {
                    FirstName = "Jan",
                    LastName = "Kowalski",
                    IsPrimary = true,
                    JobTitle = "Kierownik Budowy",
                    Company = companies[1], // BudowaX S.A.
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 600 700 800", IsPrimary = true },
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "j.kowalski@budowax.pl", IsPrimary = false }
                    }
                },
                new()
                {
                    FirstName = "Krzysztof",
                    LastName = "Lewandowski",
                    IsPrimary = false,
                    JobTitle = "Inżynier Budowy",
                    Company = companies[1], // BudowaX S.A.
                    Owner = user!,
                    ContactDetails = new List<ContactDetail>()
                },
                new()
                {
                    FirstName = "Katarzyna",
                    LastName = "Kowal",
                    IsPrimary = true,
                    JobTitle = "Dyrektor Handlowy",
                    Company = companies[2], // Huta Żelaza 'Odra'
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "k.kowal@hutaodra.pl", IsPrimary = true },
                    }
                },
                new()
                {
                    FirstName = "Agnieszka",
                    LastName = "Zielińska",
                    IsPrimary = false,
                    JobTitle = "Specjalista ds. Logistyki",
                    Company = companies[2], // Huta Żelaza 'Odra'
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "a.zielinska@hutaodra.pl", IsPrimary = true },
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 222 333 444", IsPrimary = false }
                    }
                },
                new()
                {
                    FirstName = "Magdalena",
                    LastName = "Dąbrowska",
                    IsPrimary = false,
                    JobTitle = "Dział Kontroli Jakości",
                    Company = companies[2], // Huta Żelaza 'Odra'
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "m.dabrowska@hutaodra.pl", IsPrimary = true }
                    }
                },
                new()
                {
                    FirstName = "Piotr",
                    LastName = "Wójcik",
                    IsPrimary = true,
                    JobTitle = "Właściciel",
                    Company = companies[3], // P.H.U. Konstrukcje Stalowe
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "p.wojcik@konstrukcje.pl", IsPrimary = true }
                    }
                },
                new()
                {
                    FirstName = "Tomasz",
                    LastName = "Woźniak",
                    IsPrimary = false,
                    JobTitle = "Kosztorysant",
                    Company = companies[3], // P.H.U. Konstrukcje Stalowe
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 444 555 666", IsPrimary = true },
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "t.wozniak@konstrukcje.pl", IsPrimary = false }
                    }
                },
                new()
                {
                    FirstName = "Barbara",
                    LastName = "Szymańska",
                    IsPrimary = true,
                    JobTitle = "Zaopatrzeniowiec",
                    Company = companies[4], // Mega-Stal s.c.
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "b.szymanska@megastal.pl", IsPrimary = false },
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 555 111 222", IsPrimary = true }
                    }
                },
                new()
                {
                    FirstName = "Maria",
                    LastName = "Kamińska",
                    IsPrimary = false,
                    JobTitle = "Asystentka Zarządu",
                    Company = companies[4], // Mega-Stal s.c.
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 999 888 777", IsPrimary = true }
                    }
                }
            };

            await _context.Contacts.AddRangeAsync(contacts);
            await _context.SaveChangesAsync();
            Console.WriteLine("All contacts seeded successfully.");
        }

        private async Task SeedProductsAsync()
        {
            var types = await _context.ProductTypes.ToListAsync();
            var units = await _context.UnitsOfMeasure.ToListAsync();
            var random = new Random();
            var products = new List<Product>();

            var grades = new[] { "S235JR", "S355J2", "DC01", "DX51D", "304L" };

            for (int i = 1; i <= 50; i++)
            {
                var type = types[random.Next(types.Count)];
                var unit = units[random.Next(units.Count)];
                var grade = grades[random.Next(grades.Length)];

                products.Add(new Product
                {
                    Name = $"{type.Name} {grade} Wymiar {random.Next(10, 200)}x{random.Next(10, 200)}",
                    SteelGrade = grade,
                    Thickness = random.Next(10, 500),
                    Width = random.Next(100, 2000),
                    Length = random.Next(1000, 12000),
                    Weight = random.Next(10, 5000),
                    Unit = unit,
                    PricePerUnit = random.Next(1000, 10000) * 10000,
                    StockQuantity = random.Next(0, 500),
                    ProductType = type
                });

                Console.WriteLine($"Prepared product {i}: {products.Last().Name}");
            }

            await _context.Products.AddRangeAsync(products);
            await _context.SaveChangesAsync();
            Console.WriteLine("All products seeded successfully.");
        }

        private async Task SeedDealsAndTasksAsync()
        {
            var users = await _userManager.Users.ToListAsync();
            var companies = await _context.Companies.ToListAsync();
            var currencies = await _context.Currencies.ToListAsync();
            var products = await _context.Products.ToListAsync();
            var random = new Random();

            var deals = new List<Deal>();
            var dealProducts = new List<DealProduct>();
            var tasks = new List<Tasks>();

            var dealStatuses = Enum.GetValues(typeof(DealsStatusEnum)).Cast<DealsStatusEnum>().ToArray();
            var taskStatuses = Enum.GetValues(typeof(TaskStatusEnum)).Cast<TaskStatusEnum>().ToArray();
            var taskPriorities = Enum.GetValues(typeof(TaskPriorityEnum)).Cast<TaskPriorityEnum>().ToArray();

            for (int i = 1; i <= 100; i++)
            {
                var owner = users[random.Next(users.Count)];
                var company = companies[random.Next(companies.Count)];
                var currency = currencies[random.Next(currencies.Count)];
                var status = dealStatuses[random.Next(dealStatuses.Length)];

                var deal = new Deal
                {
                    Name = $"Zamówienie hurtowe nr {i}/{DateTime.Now.Year}",
                    Value = random.Next(10000, 500000) * 10000L,
                    Status = status,
                    CloseDate = DateTime.UtcNow.AddDays(random.Next(-30, 90)),
                    Currency = currency,
                    Company = company,
                    Owner = owner
                };
                deals.Add(deal);
                Console.WriteLine($"Prepared deal {i}: {deal.Name}");

                if (random.Next(100) < 30)
                {
                    bool isPaid = random.Next(100) < 50;

                    var invoice = new Invoice
                    {
                        InvoiceNumber = $"FV/{DateTime.Now.Year}/{DateTime.Now.Month:D2}/{i:D3}",
                        TotalAmount = deal.Value,
                        PaidAmount = isPaid ? deal.Value : 0,
                        IssueDate = deal.CloseDate.AddDays(-14), 
                        DueDate = deal.CloseDate, 
                        PaymentDate = isPaid ? deal.CloseDate.AddDays(-2) : null,
                        Currency = deal.Currency,
                        Company = deal.Company,
                        Deal = deal
                    };

                    await _context.Invoices.AddAsync(invoice);
                }

                int itemsCount = random.Next(1, 5);
                for (int j = 0; j < itemsCount; j++)
                {
                    var product = products[random.Next(products.Count)];
                    dealProducts.Add(new DealProduct
                    {
                        Deal = deal,
                        Product = product,
                        Quantity = random.Next(1, 50),
                        UnitPrice = product.PricePerUnit
                    });

                    Console.WriteLine("  - Added product to deal: " + product.Name);
                }

                for (int t = 1; t <= 2; t++)
                {
                    tasks.Add(new Tasks
                    {
                        Title = $"Zadanie {t} - Zamówienie nr {i}",
                        Description = t == 1 ? "Przygotować dokumentację wstępną." : "Skontaktować się w celu potwierdzenia warunków.",
                        DueAt = DateTime.UtcNow.AddDays(random.Next(1, 14)),
                        AssignedTo = owner,
                        Deal = deal,
                        Status = taskStatuses[random.Next(taskStatuses.Length)],
                        Priority = taskPriorities[random.Next(taskPriorities.Length)]
                    });

                    Console.WriteLine($"  - Added task {t} to deal: {deal.Name}");
                }
            }

            await _context.Deals.AddRangeAsync(deals);
            await _context.DealProducts.AddRangeAsync(dealProducts);
            await _context.Tasks.AddRangeAsync(tasks);
            await _context.SaveChangesAsync();
            Console.WriteLine("All deals and tasks seeded successfully.");
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


            Console.WriteLine($"Generated random point: ({lat}, {lng})");

            return new Point(lng, lat) { SRID = 4326 };
        }
    }
}
