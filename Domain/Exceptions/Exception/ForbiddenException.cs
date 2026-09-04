using Domain.Constants;

namespace Domain.Exceptions.Exception
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message = "You do not have permission to perform this action.")
            : base(message, ErrorCodes.UnauthorizedAccess, 403)
        {
        }
    }
}
