namespace Domain.Exceptions
{
    public abstract class AppException : System.Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }

        protected AppException(string message, string errorCode, int statusCode = 500)
            : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
        }
    }
}
