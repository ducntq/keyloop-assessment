using KeyloopScheduler.Api.Contracts;
using KeyloopScheduler.Domain.Abstractions;
using KeyloopScheduler.Domain.Abstractions.Models;
using KeyloopScheduler.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace KeyloopScheduler.Api.Controllers;

/// <summary>
/// Booking and cancellation endpoints for appointments.
/// </summary>
[ApiController]
[Route("api/appointments")]
[Produces("application/json")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IAppointmentBookingService _bookingService;
    private readonly IAppointmentRepository _appointmentRepository;

    public AppointmentsController(
        IAppointmentBookingService bookingService,
        IAppointmentRepository appointmentRepository)
    {
        _bookingService = bookingService;
        _appointmentRepository = appointmentRepository;
    }

    /// <summary>
    /// Books an appointment, reserving both a service bay and a qualified technician.
    /// </summary>
    /// <remarks>
    /// Returns 409 Conflict when either resource is already booked for the window,
    /// including when two clients race for the same slot.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppointmentResponse>> CreateAsync(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var vin = new Vin(request.Vin);

        var bookingRequest = new BookingRequest(
            request.DealershipId,
            request.ServiceTypeId,
            request.ServiceBayId,
            request.TechnicianId,
            vin,
            request.StartTimeUtc);

        var result = await _bookingService.BookAsync(bookingRequest, cancellationToken);
        var response = AppointmentResponse.From(result);

        return CreatedAtRoute("GetAppointmentById", new { id = response.AppointmentId }, response);
    }

    /// <summary>
    /// Retrieves a single appointment by its identifier.
    /// </summary>
    [HttpGet("{id:guid}", Name = "GetAppointmentById")]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, cancellationToken);

        if (appointment is null)
        {
            return NotFound();
        }

        return Ok(AppointmentResponse.From(appointment));
    }

    /// <summary>
    /// Cancels an appointment and releases its bay and technician.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        await _bookingService.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}
