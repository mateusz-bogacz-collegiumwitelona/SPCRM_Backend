using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Seeders
{
    public class DataSeeder
    {
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private AppDbContext _context;

        public DataSeeder (
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

            if (isUserExist == null )
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
    } 
}
