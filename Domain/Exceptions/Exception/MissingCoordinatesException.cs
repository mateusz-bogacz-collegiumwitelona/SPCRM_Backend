using Domain.Constants;
using Microsoft.AspNetCore.Http;

namespace Domain.Exceptions.Exception
{
    public class MissingCoordinatesException : AppException
    {
        public MissingCoordinatesException(string message = "Company address has missing coordinates.")
            : base(message, ErrorCodes.MissingCoordinates ,StatusCodes.Status500InternalServerError)
        {
        }
    }
}
