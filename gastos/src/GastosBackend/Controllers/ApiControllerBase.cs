using System.Security.Claims;
using GastosBackend.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace GastosBackend.Presentation.Controllers
{
    /// <summary>Base class of the controllers that work on behalf of the signed-in user.</summary>
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>
        /// Id of the authenticated user, taken from the token. The token is issued by this API, so an id that is
        /// missing or not a Guid means the token is not one of ours: it is treated as an invalid session (401).
        /// </summary>
        protected Guid UserId
        {
            get
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(claim, out var userId))
                {
                    throw new UnauthorizedException("La sesión no es válida.");
                }

                return userId;
            }
        }
    }
}
