using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers;

[Authorize]
[ApiController]
[Route("api/user-preferences")]
public class UserPreferencesController : ControllerBase
{
    private readonly VinhKhanhAudioGuideContext _context;

    public UserPreferencesController(VinhKhanhAudioGuideContext context)
    {
        _context = context;
    }

    [HttpGet("language")]
    public async Task<IActionResult> GetLanguageAsync()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var preference = await _context.UserPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId.Value);

        return Ok(new LanguageResponse
        {
            LanguageCode = preference?.PreferredLanguage
        });
    }

    [HttpPut("language")]
    public async Task<IActionResult> UpdateLanguageAsync([FromBody] LanguageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LanguageCode))
        {
            return BadRequest("Ngôn ngữ không hợp lệ.");
        }

        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var normalized = request.LanguageCode.Trim();
        var preference = await _context.UserPreferences
            .FirstOrDefaultAsync(x => x.UserId == userId.Value);

        if (preference == null)
        {
            preference = new UserPreference
            {
                UserId = userId.Value,
                PreferredLanguage = normalized,
                UpdatedAt = DateTime.Now
            };
            _context.UserPreferences.Add(preference);
        }
        else
        {
            preference.PreferredLanguage = normalized;
            preference.UpdatedAt = DateTime.Now;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    private int? GetUserId()
    {
        var userIdValue = User.FindFirstValue("UserId");
        return int.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : null;
    }

    public class LanguageRequest
    {
        public string LanguageCode { get; set; } = string.Empty;
    }

    public class LanguageResponse
    {
        public string? LanguageCode { get; set; }
    }
}
