using System.Security.Claims;

namespace Api.Controllers.Base
{
    public abstract class AuthControllerBase : BaseController
    {
        protected Guid CurrentUserId
        {
            get
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return Guid.Parse(userId!);
            }
        }
    }
}
