using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhApi.Models; // Thay VinhKhanhApi bằng tên Project của bạn nếu khác

namespace VinhKhanhApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PoisController : ControllerBase
    {
        private readonly VinhKhanhAudioGuideContext _context;

        public PoisController(VinhKhanhAudioGuideContext context)
        {
            _context = context;
        }

        // 1. LẤY TẤT CẢ QUÁN ĂN (Để hiện lên bản đồ)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Poi>>> GetPois()
        {
            return await _context.Pois
            .Include(p => p.Poilocalizations)
                .ToListAsync();
        }

        // 2. LẤY CHI TIẾT 1 QUÁN (Kèm thực đơn và thuyết minh)
        [HttpGet("{id}")]
        public async Task<ActionResult<Poi>> GetPoi(int id)
        {
            var poi = await _context.Pois
                .Include(p => p.Poilocalizations)
                .Include(p => p.Menus)
                .FirstOrDefaultAsync(p => p.Poiid == id);

            if (poi == null)
            {
                return NotFound();
            }

            return poi;
        }
    }
}