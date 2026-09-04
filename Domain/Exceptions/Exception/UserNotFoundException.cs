using Domain.Constants;

namespace Domain.Exceptions.Exception
{
    public class UserNotFoundException : AppException
    {
        public UserNotFoundException(Guid userId)
            : base($"Data integrity violation: User with ID '{userId}' does not exist.", ErrorCodes.UserNotFound, 500)
        {
        }
    }
}
