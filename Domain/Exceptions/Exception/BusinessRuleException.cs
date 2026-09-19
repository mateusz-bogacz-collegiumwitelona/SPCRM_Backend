using Domain.Constants;

namespace Domain.Exceptions.Exception
{
    public class BusinessRuleException : AppException
    {
        public BusinessRuleException(string message)
            : base(message, ErrorCodes.InvalidOperation, 400)
        {
        }
    }
}
