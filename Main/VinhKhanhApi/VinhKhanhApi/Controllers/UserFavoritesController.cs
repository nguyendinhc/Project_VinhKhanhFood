using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers;

[Authorize]
[ApiController]
[Route("api/user-favorites")]
public class UserFavoritesController : ControllerBase
{
    private readonly VinhKhanhAudioGuideContext _context;

    public UserFavoritesController(VinhKhanhAudioGuideContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetFavoritesAsync()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var favorites = await _context.UserFavorites
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.Poiid)
            .ToListAsync();

        return Ok(favorites);
    }

    [HttpPut("{poiId:int}")]
    public async Task<IActionResult> AddFavoriteAsync(int poiId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var exists = await _context.UserFavorites
            .AnyAsync(x => x.UserId == userId.Value && x.Poiid == poiId);
        if (!exists)
        {
            _context.UserFavorites.Add(new UserFavorite
            {
                UserId = userId.Value,
                Poiid = poiId,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpDelete("{poiId:int}")]
    public async Task<IActionResult> RemoveFavoriteAsync(int poiId)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var existing = await _context.UserFavorites
            .FirstOrDefaultAsync(x => x.UserId == userId.Value && x.Poiid == poiId);
        if (existing != null)
        {
            _context.UserFavorites.Remove(existing);
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    private int? GetUserId()
    {
        var userIdValue = User.FindFirstValue("UserId");
        return int.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : null;
    }
}
