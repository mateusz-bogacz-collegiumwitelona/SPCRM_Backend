using Microsoft.AspNetCore.Http;

namespace Services.Accessors
{
    public class HttpCancellationTokenAccessor : ICancellationTokenAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpCancellationTokenAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public CancellationToken Token => _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
    }
}
