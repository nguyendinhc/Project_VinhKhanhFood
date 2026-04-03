using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers
{
    [Route("api/menu")]
    [ApiController]
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
            var menus = await _context.Menus
                .AsNoTracking()
                .Include(m => m.Poi)
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

        public class MenuListItemDto
        {
            public int MenuId { get; set; }
            public int? Poiid { get; set; }
            public string FoodName { get; set; } = string.Empty;
            public decimal? Price { get; set; }
            public string? Image { get; set; }
            public string? PoiName { get; set; }
        }
    }
}
