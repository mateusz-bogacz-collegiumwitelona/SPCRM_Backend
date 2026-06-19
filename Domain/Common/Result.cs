namespace Domain.Common
{
    public class Result
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public string? ErrorCode { get; set; }
        public int StatusCode { get; set; }
        public List<string>? Errors { get; set; }

        public static Result Success(string message, int statusCode)
            => new Result
            {
                Message = message,
                StatusCode = statusCode,
                IsSuccess = true
            };

        public static Result Failure(string message, string errorCode, int statusCode, List<string>? errors = null)
            => new Result
            {
                Message = message,
                StatusCode = statusCode,
                ErrorCode = errorCode,
                IsSuccess = false,
                Errors = errors ?? new List<string>()
            };

    }

    public class Result<T> : Result
    {
        public T? Data { get; set; }

        public static Result<T> Success(string message, int statusCode, T? data = default)
            => new Result<T>
            {
                Message = message,
                StatusCode = statusCode,
                IsSuccess = true,
                Data = data
            };

        public static Result<T> Failure(string message, string errorCode, int statusCode, List<string>? errors = null, T? data = default)
            => new Result<T>
            {
                Message = message,
                StatusCode = statusCode,
                ErrorCode = errorCode,
                IsSuccess = false,
                Errors = errors ?? new List<string>(),
                Data = data
            };

        public static Result<T> Failure(string message, int statusCode, List<string>? errors = null, T? data = default)
            => new Result<T>
            {
                Message = message,
                StatusCode = statusCode,
                IsSuccess = false,
                Errors = errors ?? new List<string>(),
                Data = data
            };
    }
}
