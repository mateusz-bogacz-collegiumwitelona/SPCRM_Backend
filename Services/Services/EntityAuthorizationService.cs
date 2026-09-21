using Domain.Constants;
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
             => currentUserId == resourceOwnerId || await CanAccessAsync(currentUserId);

        public async Task<bool> CanAccessAsync(Guid userId)
            => await _context.UserRoles
                .AsNoTracking()
                .AnyAsync(ur => ur.UserId == userId &&
                    _context.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == BusinessConstants.ManagerNormalized));
    }
}
