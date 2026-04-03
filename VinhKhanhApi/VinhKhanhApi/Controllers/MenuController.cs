using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers
{
    [Route("api/menu")]
    [ApiController]
    [Authorize]
    public class MenuController : ControllerBase
    {
        private readonly VinhKhanhAudioGuideContext _context;

        public MenuController(VinhKhanhAudioGuideContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MenuListItemDto>>> GetAllAsync()
        {
            var query = _context.Menus
                .AsNoTracking()
                .Include(m => m.Poi)
                .AsQueryable();

            if (User.IsInRole("Owner"))
            {
                if (!TryGetUserId(out var userId))
                {
                    return Forbid();
                }

                var ownerPoiIds = await GetOwnerPoiIdsAsync(userId);

                query = query.Where(m => m.Poiid != null && ownerPoiIds.Contains(m.Poiid.Value));
            }

            var menus = await query
                .OrderBy(m => m.MenuId)
                .Select(m => new MenuListItemDto
                {
                    MenuId = m.MenuId,
                    Poiid = m.Poiid,
                    FoodName = m.FoodName,
                    Price = m.Price,
                    Image = m.Image,
                    PoiName = m.Poi != null ? m.Poi.Name : null
                })
                .ToListAsync();

            return Ok(menus);
        }

        [HttpPost]
        public async Task<ActionResult<MenuListItemDto>> CreateAsync([FromBody] MenuUpsertDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FoodName))
            {
                return BadRequest("Tên món không ???c ?? tr?ng.");
            }

            if (request.Poiid == null)
            {
                return BadRequest("Vui lòng ch?n quán.");
            }

            if (User.IsInRole("Owner"))
            {
                if (!TryGetUserId(out var userId))
                {
                    return Forbid();
                }

                var ownerPoiIds = await GetOwnerPoiIdsAsync(userId);
                if (!ownerPoiIds.Contains(request.Poiid.Value))
                {
                    return Forbid();
                }

                if (!await IsPoiActiveAsync(request.Poiid.Value))
                {
                    return BadRequest("Qu?n ?ang t?m ?n, kh?ng th? thao t?c menu.");
                }
            }

            var poiExists = await _context.Pois.AnyAsync(p => p.Poiid == request.Poiid.Value);
            if (!poiExists)
            {
                return BadRequest("Quán không t?n t?i.");
            }

            var menu = new Menu
            {
                Poiid = request.Poiid,
                FoodName = request.FoodName.Trim(),
                Price = request.Price,
                Image = request.Image
            };

            _context.Menus.Add(menu);
            await _context.SaveChangesAsync();

            var poiName = await _context.Pois
                .Where(p => p.Poiid == menu.Poiid)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();

            return Ok(new MenuListItemDto
            {
                MenuId = menu.MenuId,
                Poiid = menu.Poiid,
                FoodName = menu.FoodName,
                Price = menu.Price,
                Image = menu.Image,
                PoiName = poiName
            });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAsync(int id, [FromBody] MenuUpsertDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.FoodName))
            {
                return BadRequest("Tên món không ???c ?? tr?ng.");
            }

            var menu = await _context.Menus.FirstOrDefaultAsync(m => m.MenuId == id);
            if (menu == null)
            {
                return NotFound("Không tìm th?y món ?n.");
            }

            if (request.Poiid == null)
            {
                return BadRequest("Vui lòng ch?n quán.");
            }

            if (User.IsInRole("Owner"))
            {
                if (!TryGetUserId(out var userId))
                {
                    return Forbid();
                }

                var ownerPoiIds = await GetOwnerPoiIdsAsync(userId);
                if (!ownerPoiIds.Contains(menu.Poiid ?? 0) || !ownerPoiIds.Contains(request.Poiid.Value))
                {
                    return Forbid();
                }

                if (!await IsPoiActiveAsync(menu.Poiid ?? 0) || !await IsPoiActiveAsync(request.Poiid.Value))
                {
                    return BadRequest("Qu?n ?ang t?m ?n, kh?ng th? thao t?c menu.");
                }
            }

            var poiExists = await _context.Pois.AnyAsync(p => p.Poiid == request.Poiid.Value);
            if (!poiExists)
            {
                return BadRequest("Quán không t?n t?i.");
            }

            menu.FoodName = request.FoodName.Trim();
            menu.Price = request.Price;
            menu.Image = request.Image;
            menu.Poiid = request.Poiid;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            var menu = await _context.Menus.FirstOrDefaultAsync(m => m.MenuId == id);
            if (menu == null)
            {
                return NotFound("Không tìm th?y món ?n.");
            }

            if (User.IsInRole("Owner"))
            {
                if (!TryGetUserId(out var userId))
                {
                    return Forbid();
                }

                var ownerPoiIds = await GetOwnerPoiIdsAsync(userId);
                if (!ownerPoiIds.Contains(menu.Poiid ?? 0))
                {
                    return Forbid();
                }

                if (!await IsPoiActiveAsync(menu.Poiid ?? 0))
                {
                    return BadRequest("Qu?n ?ang t?m ?n, kh?ng th? thao t?c menu.");
                }
            }

            _context.Menus.Remove(menu);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool TryGetUserId(out int userId)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out userId);
        }

        private async Task<List<int>> GetOwnerPoiIdsAsync(int userId)
        {
            return await _context.PoiSubmissions
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.Poiid != null)
                .Select(s => s.Poiid!.Value)
                .Distinct()
                .ToListAsync();
        }

        private async Task<bool> IsPoiActiveAsync(int poiId)
        {
            var status = await _context.Pois
                .AsNoTracking()
                .Where(p => p.Poiid == poiId)
                .Select(p => p.Status)
                .FirstOrDefaultAsync();

            return string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "1", StringComparison.OrdinalIgnoreCase);
        }

        public class MenuListItemDto
        {
            public int MenuId { get; set; }
            public int? Poiid { get; set; }
            public string FoodName { get; set; } = string.Empty;
            public decimal? Price { get; set; }
            public string? Image { get; set; }
            public string? PoiName { get; set; }
        }

        public class MenuUpsertDto
        {
            public int? Poiid { get; set; }
            public string FoodName { get; set; } = string.Empty;
            public decimal? Price { get; set; }
            public string? Image { get; set; }
        }
    }
}
