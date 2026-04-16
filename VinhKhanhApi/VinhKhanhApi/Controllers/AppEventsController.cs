using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers;

[Route("api/events")]
[ApiController]
public class AppEventsController : ControllerBase
{
    private readonly VinhKhanhAudioGuideContext _context;

    public AppEventsController(VinhKhanhAudioGuideContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateAppEventRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.EventType))
        {
            return BadRequest("Thiếu DeviceId hoặc EventType.");
        }

        var deviceId = request.DeviceId.Trim();
        var eventType = request.EventType.Trim();

        if (deviceId.Length > 255 || eventType.Length > 50)
        {
            return BadRequest("DeviceId hoặc EventType quá dài.");
        }

        var entity = new AppEventLog
        {
            DeviceId = deviceId,
            EventType = eventType,
            QrCode = string.IsNullOrWhiteSpace(request.QrCode) ? null : request.QrCode.Trim(),
            Poiid = request.Poiid,
            CreatedAt = DateTime.Now
        };

        _context.AppEventLogs.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Logged", entity.EventId });
    }

    public class CreateAppEventRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string? QrCode { get; set; }
        public int? Poiid { get; set; }
    }
}

