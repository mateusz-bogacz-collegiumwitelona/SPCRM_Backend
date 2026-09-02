namespace Services.Interfaces
{
    public interface IEntityAuthorizationService
    {
        Task<bool> CanModifyAsync(Guid currentUserId, Guid resourceOwnerId);
    }
}
