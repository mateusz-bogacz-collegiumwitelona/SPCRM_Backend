using Domain.Constants;

namespace Domain.Exceptions.Exception
{
    public class DataCorruptionException : AppException
    {
        public DataCorruptionException(string message)
            : base(message, ErrorCodes.InternalError, 500)
        {
        }
    }
}
