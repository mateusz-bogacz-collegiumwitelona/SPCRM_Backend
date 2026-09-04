using Microsoft.AspNetCore.Http;

namespace Domain.Exceptions.Exception
{
    public class MissingUserRoleException : AppException
    {
        public MissingUserRoleException(string message = "One or more users have no assigned role.")
             : base(message, "DataIntegrityError", StatusCodes.Status500InternalServerError)
        {
        }

        public MissingUserRoleException(Guid userId)
            : base($"User with ID '{userId}' has no assigned role.", "DataIntegrityError", StatusCodes.Status500InternalServerError)
        {
        }
    }
}
