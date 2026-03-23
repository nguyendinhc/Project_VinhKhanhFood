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
            var pois = await _context.Pois
                .Include(p => p.Poilocalizations)
                .ToListAsync();

            NormalizePoiThumbnails(pois);
            return pois;
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

            NormalizePoiThumbnail(poi);

            return poi;
        }

        // 3. THÊM MỚI 1 QUÁN
        [HttpPost]
        public async Task<ActionResult<Poi>> PostPoi([FromBody] Poi poi)
        {
            if (poi == null)
            {
                return BadRequest("Dữ liệu gửi lên không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(poi.Name))
            {
                return BadRequest("Tên quán không được để trống.");
            }

            if (poi.Latitude is < -90 or > 90)
            {
                return BadRequest("Vĩ độ không hợp lệ. Giá trị phải trong khoảng -90 đến 90.");
            }

            if (poi.Longitude is < -180 or > 180)
            {
                return BadRequest("Kinh độ không hợp lệ. Giá trị phải trong khoảng -180 đến 180.");
            }

            poi.CreatedAt ??= DateTime.Now;
            poi.Status ??= "Pending";

            _context.Pois.Add(poi);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPoi), new { id = poi.Poiid }, poi);
        }

        // 4. CẬP NHẬT 1 QUÁN
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPoi(int id, [FromBody] Poi poi)
        {
            if (poi == null || id != poi.Poiid)
            {
                return BadRequest("Dữ liệu cập nhật không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(poi.Name))
            {
                return BadRequest("Tên quán không được để trống.");
            }

            if (poi.Latitude is < -90 or > 90)
            {
                return BadRequest("Vĩ độ không hợp lệ. Giá trị phải trong khoảng -90 đến 90.");
            }

            if (poi.Longitude is < -180 or > 180)
            {
                return BadRequest("Kinh độ không hợp lệ. Giá trị phải trong khoảng -180 đến 180.");
            }

            var existingPoi = await _context.Pois.FirstOrDefaultAsync(p => p.Poiid == id);
            if (existingPoi == null)
            {
                return NotFound();
            }

            existingPoi.Name = poi.Name;
            existingPoi.Latitude = poi.Latitude;
            existingPoi.Longitude = poi.Longitude;
            existingPoi.Radius = poi.Radius;
            existingPoi.Thumbnail = poi.Thumbnail;
            existingPoi.Status = poi.Status;
            existingPoi.CreatedAt = poi.CreatedAt ?? existingPoi.CreatedAt;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 5. XÓA 1 QUÁN
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePoi(int id)
        {
            var poi = await _context.Pois.FirstOrDefaultAsync(p => p.Poiid == id);
            if (poi == null)
            {
                return NotFound();
            }

            _context.Pois.Remove(poi);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private void NormalizePoiThumbnails(IEnumerable<Poi> pois)
        {
            foreach (var poi in pois)
            {
                NormalizePoiThumbnail(poi);
            }
        }

        private void NormalizePoiThumbnail(Poi poi)
        {
            if (string.IsNullOrWhiteSpace(poi.Thumbnail))
            {
                return;
            }

            if (Uri.TryCreate(poi.Thumbnail, UriKind.Absolute, out _))
            {
                return;
            }

            var thumbnailPath = poi.Thumbnail.Trim();
            if (!thumbnailPath.StartsWith('/'))
            {
                thumbnailPath = "/" + thumbnailPath.TrimStart('~', '/');
            }

            poi.Thumbnail = $"{Request.Scheme}://{Request.Host}{thumbnailPath}";
        }
    }
}