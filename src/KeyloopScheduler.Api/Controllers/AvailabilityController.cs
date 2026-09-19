using KeyloopScheduler.Api.Contracts;
using KeyloopScheduler.Domain.Abstractions;
using KeyloopScheduler.Domain.Abstractions.Models;
using Microsoft.AspNetCore.Mvc;

namespace KeyloopScheduler.Api.Controllers;

/// <summary>
/// Read-only resource availability search.
/// </summary>
[ApiController]
[Route("api/availability")]
[Produces("application/json")]
public sealed class AvailabilityController : ControllerBase
{
    private readonly IResourceAvailabilityQuery _availabilityQuery;

    public AvailabilityController(IResourceAvailabilityQuery availabilityQuery)
    {
        _availabilityQuery = availabilityQuery;
    }

    /// <summary>
    /// Returns every slot on the requested day where BOTH a service bay and a
    /// technician certified for the service type are free for the full duration.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilitySlotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AvailabilitySlotResponse>>> SearchAsync(
        [FromQuery] AvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var query = new AvailabilityQuery(request.DealershipId, request.ServiceTypeId, request.Date);

        var slots = await _availabilityQuery.FindAvailableSlotsAsync(query, cancellationToken);

        return Ok(slots.Select(AvailabilitySlotResponse.From).ToList());
    }
}
