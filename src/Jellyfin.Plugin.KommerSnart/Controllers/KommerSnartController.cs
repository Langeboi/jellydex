using Jellyfin.Plugin.KommerSnart.Models;
using Jellyfin.Plugin.KommerSnart.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.KommerSnart.Controllers;

[ApiController]
[Route("KommerSnart")]
[Authorize]
public sealed class KommerSnartController : ControllerBase
{
    private readonly CalendarService _calendarService;

    public KommerSnartController(CalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    [HttpGet("Calendar")]
    [ProducesResponseType(typeof(CalendarResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CalendarResponse>> GetCalendar(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _calendarService.GetCalendarAsync(false, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Kunne ikke hente kalenderen",
                detail: exception.Message);
        }
    }

    [HttpPost("Admin/Test")]
    [Authorize(Policy = "RequiresElevation")]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        try
        {
            await _calendarService.TestConnectionAsync(cancellationToken).ConfigureAwait(false);
            return Ok(new { success = true, message = "Tilkoblingen til Seerr virker." });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                message = exception.Message
            });
        }
    }

    [HttpPost("Admin/Refresh")]
    [Authorize(Policy = "RequiresElevation")]
    public async Task<ActionResult<CalendarResponse>> Refresh(CancellationToken cancellationToken)
    {
        return Ok(await _calendarService.GetCalendarAsync(true, cancellationToken).ConfigureAwait(false));
    }
}
