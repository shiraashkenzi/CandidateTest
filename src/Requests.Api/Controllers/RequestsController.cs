using Microsoft.AspNetCore.Mvc;
using Requests.Api.Identity;
using Requests.Application.Common;
using Requests.Application.Requests;

namespace Requests.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RequestsController : ControllerBase
{
    private readonly IRequestService _service;

    public RequestsController(IRequestService service)
    {
        _service = service;
    }

    /// <summary>
    /// Searches requests visible to the caller.
    /// </summary>
    /// <remarks>
    /// For the exercise, the current user is supplied through headers — see
    /// <see cref="CurrentUserResolver"/>:
    /// <code>
    /// X-User-Id:  integer greater than zero (required)
    /// X-Is-Admin: true|false                (optional)
    /// </code>
    /// All filters are optional. Repeat the key for multiple statuses:
    /// <c>?statuses=New&amp;statuses=InProgress</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<RequestDto>>> Search(
        [FromQuery] RequestSearchParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!CurrentUserResolver.TryResolve(Request, out var currentUser, out var error, out var isMissing))
        {
            // No identity asserted at all (401) versus asserted but unusable (400).
            if (isMissing)
            {
                return Problem(
                    detail: error,
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Unauthorized");
            }

            ModelState.AddModelError(CurrentUserResolver.UserIdHeader, error);
            return ValidationProblem(ModelState);
        }

        RejectCommaCombinedEnums();

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _service.SearchAsync(
            parameters,
            currentUser.UserId,
            currentUser.IsAdministrator,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Rejects <c>?statuses=1,2</c> and <c>?requestType=1,2</c>, which model binding would otherwise
    /// accept as a bitwise enum combination and answer with the wrong rows and a 200.
    /// </summary>
    private void RejectCommaCombinedEnums()
    {
        if (Request.Query[nameof(RequestSearchParameters.Statuses)].Any(RequestSearchParameters.IsCommaCombinedValue))
        {
            ModelState.AddModelError(
                nameof(RequestSearchParameters.Statuses),
                "Send multiple statuses as repeated query parameters (?statuses=1&statuses=2), " +
                "not as a comma-separated value.");
        }

        if (Request.Query[nameof(RequestSearchParameters.RequestType)].Any(RequestSearchParameters.IsCommaCombinedValue))
        {
            ModelState.AddModelError(
                nameof(RequestSearchParameters.RequestType),
                "requestType accepts a single value; comma-separated values are not supported.");
        }
    }
}
