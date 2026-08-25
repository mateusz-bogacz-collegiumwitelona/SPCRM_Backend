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
            if (!await _context.SteelGrades.AnyAsync()) await SeedSteelGradesAsync();

            if (!await _context.Companies.AnyAsync()) await SeedCompaniesAndContactsAsync();
            if (!await _context.Products.AnyAsync()) await SeedProductsAsync();

            if (!await _context.Promotions.AnyAsync()) await SeedPromotionsAsync();

            if (!await _context.Deals.AnyAsync()) await SeedDealsAndTasksAsync();
            if (!await _context.Tasks.AnyAsync(t => t.DealId == null)) await SeedStandaloneTasksAsync();
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
                    !await _roleManager.RoleExistsAsync("User"))
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
            string lastName)
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

        private async Task SeedSteelGradesAsync()
        {
            var steelGrades = new List<SteelGrade>
            {
                new() { Name = "S235JR", Standard = "EN 10025-2", Density = 7850 },
                new() { Name = "S355J2", Standard = "EN 10025-2", Density = 7850 },
                new() { Name = "DC01", Standard = "EN 10130", Density = 7850 },
                new() { Name = "DX51D", Standard = "EN 10346", Density = 7850 },
                new() { Name = "304L", Standard = "EN 10088-2", Density = 7900 },
                new() { Name = "316L", Standard = "EN 10088-2", Density = 8000 },
                new() { Name = "AW6060", Standard = "EN 573-3", Density = 2700 },
                new() { Name = "C45", Standard = "EN 10083-2", Density = 7850 }
            };

            await _context.SteelGrades.AddRangeAsync(steelGrades);
            await _context.SaveChangesAsync();
            Console.WriteLine("All steel grades seeded successfully.");
        }

        private async Task SeedProductsAsync()
        {
            var units = await _context.UnitsOfMeasure.ToListAsync();
            var steelGrades = await _context.SteelGrades.ToListAsync();
            var random = new Random();
            var products = new List<Product>();

            var categories = Enum.GetValues<ProductCategoryEnum>();

            for (int i = 1; i <= 60; i++)
            {
                var unit = units[random.Next(units.Count)];
                var grade = steelGrades[random.Next(steelGrades.Count)];
                var category = categories[random.Next(categories.Length)];

                int thickness = 0;
                int width = 0;
                int length = 0;
                int? diameter = null;

                // Generowanie spójnych parametrów geometrycznych dla poszczególnych kategorii (wartości * 10)
                switch (category)
                {
                    case ProductCategoryEnum.Sheet:
                        thickness = random.Next(10, 300);       // 1.0 - 30.0 mm
                        width = random.Next(10000, 20000);      // 1000 - 2000 mm
                        length = random.Next(20000, 60000);     // 2000 - 6000 mm
                        break;

                    case ProductCategoryEnum.Pipe:
                        diameter = random.Next(213, 5080);      // fi 21.3 - 508.0 mm
                        thickness = random.Next(20, 125);       // 2.0 - 12.5 mm
                        length = random.Next(60000, 120000);    // 6000 - 12000 mm
                        break;

                    case ProductCategoryEnum.Bar:
                        bool isRound = random.Next(100) < 60;
                        if (isRound)
                        {
                            diameter = random.Next(60, 2000);   // fi 6.0 - 200.0 mm
                        }
                        else
                        {
                            width = random.Next(200, 1500);     // 20.0 - 150.0 mm
                            thickness = random.Next(50, 400);   // 5.0 - 40.0 mm
                        }
                        length = random.Next(30000, 60000);     // 3000 - 6000 mm
                        break;

                    case ProductCategoryEnum.Profile:
                    case ProductCategoryEnum.Beam:
                        width = random.Next(200, 3000);         // 20.0 - 300.0 mm
                        thickness = random.Next(20, 100);       // 2.0 - 10.0 mm
                        length = random.Next(60000, 120000);    // 6000 - 12000 mm
                        break;

                    case ProductCategoryEnum.Wire:
                        diameter = random.Next(10, 120);        // fi 1.0 - 12.0 mm
                        length = random.Next(10000, 100000);    // 1000 - 10000 mm
                        break;

                    case ProductCategoryEnum.Mesh:
                        thickness = random.Next(40, 120);       // drut fi 4.0 - 12.0 mm
                        width = random.Next(21500, 24000);      // 2150 - 2400 mm
                        length = random.Next(50000, 60000);     // 5000 - 6000 mm
                        break;

                    case ProductCategoryEnum.Fitting:
                    case ProductCategoryEnum.Other:
                    default:
                        diameter = random.Next(100) < 50 ? random.Next(200, 1000) : null;
                        thickness = random.Next(20, 200);
                        width = random.Next(100, 1000);
                        length = random.Next(1000, 60000);
                        break;
                }

                products.Add(new Product
                {
                    Name = $"{category} {grade.Name} #{i}",
                    SteelGradeId = grade.Id,
                    SteelGrade = grade,
                    Thickness = thickness,
                    Width = width,
                    Length = length,
                    Diameter = diameter,
                    Weight = random.Next(100, 50000),             // waga
                    Unit = unit,
                    UnitId = unit.Id,
                    PricePerUnit = random.Next(1000, 10000) * 10000L,
                    StockQuantity = random.Next(5, 500),
                    Category = category
                });

                Console.WriteLine($"Prepared product {i}: {products.Last().Name}");
            }

            await _context.Products.AddRangeAsync(products);
            await _context.SaveChangesAsync();
            Console.WriteLine("All products seeded successfully.");
        }

        private async Task SeedCompaniesAndContactsAsync()
        {
            var user = await _userManager.FindByEmailAsync("user@example.pl")
                ?? throw new InvalidOperationException("Seeding failed: Default user not found.");
            var manager = await _userManager.FindByEmailAsync("manager@example.pl")
                ?? throw new InvalidOperationException("Seeding failed: Default admin not found.");

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
                    Company = companies[0],
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "a.nowak@stalmet.pl", IsPrimary = true, Label = "Służbowy" },
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 111 222 333", IsPrimary = false, Label = "Komórka bezpośrednia" },
                        new() { Type = ContactDetailTypeEnum.LINKEDIN, Value = "https://www.linkedin.com/in/andrzej-nowak-stal", IsPrimary = false, Label = "Profil zawodowy" }
                    },
                    Notes = new List<ContactNote>
                    {
                        new() { Title = "Uwaga na negocjacje", Content = "Bardzo twardy negocjator. Lubi konkrety, nie znosi lania wody.", Author = manager! },
                        new() { Title = "Preferencje", Content = "Prosił, żeby dzwonić wyłącznie po godzinie 14:00.", Author = user! }
                    }
                },
                new()
                {
                    FirstName = "Anna",
                    LastName = "Wiśniewska",
                    IsPrimary = false,
                    JobTitle = "Główna Księgowa",
                    Company = companies[0],
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "a.wisniewska@stalmet.pl", IsPrimary = true, Label = "Do e-faktur" },
                        new() { Type = ContactDetailTypeEnum.PHONE, Value = "+48 32 700 88 11", IsPrimary = false, Label = "Stacjonarny (wewn. 204)" }
                    },
                    Notes = new List<ContactNote>
                    {
                        new() {
                            Title = "Problemy z płatnościami",
                            Content = "Pani Anna ostrzegała, że w tym miesiącu mogą mieć kilkudniowy poślizg z przelewami z powodu audytu.",
                            Author = manager!
                        }
                    }
                },
                new()
                {
                    FirstName = "Marcin",
                    LastName = "Zieliński",
                    IsPrimary = false,
                    JobTitle = "Kierownik Magazynu",
                    Company = companies[0],
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 777 888 999", IsPrimary = true, Label = "Magazyn wysyłkowy" }
                    },
                    Notes = new List<ContactNote>
                    {
                        new() { Title = "Buc", Content = "Buc wredny", Author = manager! }
                    }
                },
                new()
                {
                    FirstName = "Jan",
                    LastName = "Kowalski",
                    IsPrimary = true,
                    JobTitle = "Kierownik Budowy",
                    Company = companies[1],
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 600 700 800", IsPrimary = true, Label = "Kontener kierownika" },
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "j.kowalski@budowax.pl", IsPrimary = false, Label = "Biuro budowy" }
                    }
                },
                new()
                {
                    FirstName = "Krzysztof",
                    LastName = "Lewandowski",
                    IsPrimary = false,
                    JobTitle = "Inżynier Budowy",
                    Company = companies[1],
                    Owner = user!,
                    ContactDetails = new List<ContactDetail>()
                },
                new()
                {
                    FirstName = "Katarzyna",
                    LastName = "Kowal",
                    IsPrimary = true,
                    JobTitle = "Dyrektor Handlowy",
                    Company = companies[2],
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "k.kowal@hutaodra.pl", IsPrimary = true, Label = "Zapytania ofertowe" },
                        new() { Type = ContactDetailTypeEnum.PHONE, Value = "+48 91 400 55 00", IsPrimary = false, Label = "Sekretariat Dyrekcji" }
                    }
                },
                new()
                {
                    FirstName = "Agnieszka",
                    LastName = "Zielińska",
                    IsPrimary = false,
                    JobTitle = "Specjalista ds. Logistyki",
                    Company = companies[2],
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "a.zielinska@hutaodra.pl", IsPrimary = true, Label = "Awizacje dostaw" },
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 222 333 444", IsPrimary = false, Label = "Spedycja kolejowa" },
                        new() { Type = ContactDetailTypeEnum.FAX, Value = "+48 91 400 55 99", IsPrimary = false, Label = "Faks logistyczny" }
                    }
                },
                new()
                {
                    FirstName = "Magdalena",
                    LastName = "Dąbrowska",
                    IsPrimary = false,
                    JobTitle = "Dział Kontroli Jakości",
                    Company = companies[2],
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "m.dabrowska@hutaodra.pl", IsPrimary = true, Label = "Atesty i reklamacje" }
                    },
                    Notes = new List<ContactNote>
                    {
                        new() { Title = "Problemy z płatnościami", Content = "Mają problemy winansowe, wolą brać w kredyt", Author = user! }
                    }
                },
                new()
                {
                    FirstName = "Piotr",
                    LastName = "Wójcik",
                    IsPrimary = true,
                    JobTitle = "Właściciel",
                    Company = companies[3],
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "p.wojcik@konstrukcje.pl", IsPrimary = true, Label = "Główny" },
                        new() { Type = ContactDetailTypeEnum.LINKEDIN, Value = "https://www.linkedin.com/in/piotr-wojcik-konstrukcje", IsPrimary = false, Label = "Profil prywatny" }
                    }
                },
                new()
                {
                    FirstName = "Tomasz",
                    LastName = "Woźniak",
                    IsPrimary = false,
                    JobTitle = "Kosztorysant",
                    Company = companies[3],
                    Owner = user!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 444 555 666", IsPrimary = true, Label = "Komórka służbowa" },
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "t.wozniak@konstrukcje.pl", IsPrimary = false, Label = "Wyceny" },
                        new() { Type = ContactDetailTypeEnum.OTHER, Value = "tomasz.wozniak.teams", IsPrimary = false, Label = "MS Teams ID" }
                    }
                },
                new()
                {
                    FirstName = "Barbara",
                    LastName = "Szymańska",
                    IsPrimary = true,
                    JobTitle = "Zaopatrzeniowiec",
                    Company = companies[4],
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.EMAIL, Value = "b.szymanska@megastal.pl", IsPrimary = false, Label = "Zamówienia huta" },
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 555 111 222", IsPrimary = true, Label = "Służbowy (Pilne)" }
                    }
                },
                new()
                {
                    FirstName = "Maria",
                    LastName = "Kamińska",
                    IsPrimary = false,
                    JobTitle = "Asystentka Zarządu",
                    Company = companies[4],
                    Owner = manager!,
                    ContactDetails = new List<ContactDetail> {
                        new() { Type = ContactDetailTypeEnum.PHONE_MOBILE, Value = "+48 999 888 777", IsPrimary = true, Label = "Sekretariat" },
                        new() { Type = ContactDetailTypeEnum.OTHER, Value = "live:maria.kam_3", IsPrimary = false, Label = "Skype firmowy" }
                    }
                }
            };

            await _context.Contacts.AddRangeAsync(contacts);
            await _context.SaveChangesAsync();
            Console.WriteLine("All contacts seeded successfully.");
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

            var dealStatuses = Enum.GetValues<DealsStatusEnum>();
            var taskStatuses = Enum.GetValues<TaskStatusEnum>();
            var taskPriorities = Enum.GetValues<TaskPriorityEnum>();

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
                    Owner = owner,
                    Notes = new List<DealNote>()
                };

                if (random.Next(100) < 40)
                {
                    deal.Notes.Add(new DealNote
                    {
                        Title = "Wstępne ustalenia",
                        Content = random.Next(100) < 50
                            ? "Klient mocno naciska na dodatkowy rabat na transport."
                            : "Udało się wynegocjować lepszą marżę, w zamian za szybszą dostawę.",
                        Author = owner
                    });
                }

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
                    var task = new Tasks
                    {
                        Title = $"Zadanie {t} - Zamówienie nr {i}",
                        Description = t == 1 ? "Przygotować dokumentację wstępną." : "Skontaktować się w celu potwierdzenia warunków.",
                        DueAt = DateTime.UtcNow.AddDays(random.Next(1, 14)),
                        AssignedTo = owner,
                        Deal = deal,
                        Status = taskStatuses[random.Next(taskStatuses.Length)],
                        Priority = taskPriorities[random.Next(taskPriorities.Length)],
                        Notes = new List<TaskNote>()
                    };

                    if (random.Next(100) < 25)
                    {
                        task.Notes.Add(new TaskNote
                        {
                            Title = "Komentarz do zadania",
                            Content = "Czekam na maila zwrotnego od magazynu, żeby móc to ruszyć dalej.",
                            Author = owner
                        });
                    }

                    tasks.Add(task);
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

        private async Task SeedStandaloneTasksAsync()
        {
            var targetUsers = await _userManager.Users
                .Where(u => u.Email == "user@example.pl" || u.Email == "manager@example.pl")
                .ToListAsync();

            var contacts = await _context.Contacts.ToListAsync();
            var random = new Random();
            var tasks = new List<Tasks>();

            var taskStatuses = Enum.GetValues<TaskStatusEnum>();
            var taskPriorities = Enum.GetValues<TaskPriorityEnum>();

            var taskTitles = new[] {
                "Zadzwonić do klienta", "Wysłać ofertę", "Spotkanie handlowe",
                "Przygotować umowę", "Sprawdzić płatności", "Follow-up po spotkaniu",
                "Wysłać życzenia świąteczne", "Odpisać na maila"
            };

            foreach (var user in targetUsers)
            {
                for (int i = 1; i <= 40; i++)
                {
                    var dueAt = DateTime.UtcNow.AddDays(random.Next(-30, 30)).AddHours(random.Next(8, 16));

                    var status = taskStatuses[random.Next(taskStatuses.Length)];
                    var priority = taskPriorities[random.Next(taskPriorities.Length)];
                    var title = taskTitles[random.Next(taskTitles.Length)];

                    var contact = random.Next(100) < 50 && contacts.Any() ? contacts[random.Next(contacts.Count)] : null;

                    var task = new Tasks
                    {
                        Title = $"{title} (Test #{i})",
                        Description = "To jest automatycznie wygenerowane zadanie testowe dla kalendarza.",
                        DueAt = dueAt,
                        AssignedTo = user,
                        Status = status,
                        Priority = priority,
                        Contact = contact,
                        Deal = null,
                        Notes = new List<TaskNote>()
                    };

                    tasks.Add(task);
                    Console.WriteLine($"Prepared standalone task for {user.Email}: {task.Title}");
                }
            }

            await _context.Tasks.AddRangeAsync(tasks);
            await _context.SaveChangesAsync();
            Console.WriteLine("Standalone tasks for User and Manager seeded successfully.");
        }

        private async Task SeedPromotionsAsync()
        {
            var products = await _context.Products.ToListAsync();
            var currencies = await _context.Currencies.ToListAsync();
            var random = new Random();
            var promotions = new List<Promotion>();

            var productsForPromotion = products.OrderBy(x => random.Next()).Take(products.Count / 3).ToList();

            int i = 1;
            foreach (var product in productsForPromotion)
            {
                bool isActive = random.Next(100) < 70;
                bool isPercentageDiscount = random.Next(100) < 50;

                var promotion = new Promotion
                {
                    Name = $"Promocja {i++} na {product.Name.Split(' ')[0]}",
                    IsActive = isActive,
                    ProductId = product.Id,
                    Product = product,
                    StartDate = isActive ? DateTime.UtcNow.AddDays(-random.Next(1, 30)) : DateTime.UtcNow.AddDays(-random.Next(60, 90)),
                    EndDate = isActive ? DateTime.UtcNow.AddDays(random.Next(10, 60)) : DateTime.UtcNow.AddDays(-random.Next(1, 30))
                };

                if (isPercentageDiscount)
                {
                    promotion.DiscountPercentage = random.Next(5, 30);
                    promotion.PromotionalPrice = null;
                    promotion.CurrencyId = null;
                }
                else
                {
                    var currency = currencies[random.Next(currencies.Count)];
                    long currentPrice = product.PricePerUnit;

                    long discountAmount = (long)(currentPrice * (random.Next(10, 20) / 100.0));

                    promotion.PromotionalPrice = currentPrice - discountAmount;
                    promotion.CurrencyId = currency.Id;
                    promotion.Currency = currency;
                }

                if (random.Next(100) < 30) promotion.MinQuantity = random.Next(10, 100);

                promotions.Add(promotion);
                Console.WriteLine($"Prepared promotion: {promotion.Name} (Active: {promotion.IsActive})");
            }

            await _context.Promotions.AddRangeAsync(promotions);
            await _context.SaveChangesAsync();
            Console.WriteLine("All promotions seeded successfully.");
        }
    }
}
