using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Services.Interfaces;

namespace Services.Services
{
    public class EntityAuthorizationService : IEntityAuthorizationService
    {
        private readonly AppDbContext _context;

        public EntityAuthorizationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> CanModifyAsync(Guid currentUserId, Guid resourceOwnerId)
            => currentUserId == resourceOwnerId ||
                await (from ur in _context.UserRoles
                       join r in _context.Roles on ur.RoleId equals r.Id
                       where ur.UserId == currentUserId &&
                       (r.NormalizedName == "MANAGER")
                       select ur.UserId).AnyAsync();
    }
}
