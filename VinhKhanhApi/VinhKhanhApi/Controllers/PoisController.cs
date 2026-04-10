using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization;
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

        [HttpGet("public")]
        public async Task<ActionResult<IEnumerable<Poi>>> GetPublicPois()
        {
            var pois = await _context.Pois
                .AsNoTracking()
                .Include(p => p.Poilocalizations)
                .Where(p => p.Status != null &&
                            (p.Status.Equals("Active") ||
                             p.Status.Equals("active") ||
                             p.Status.Equals("1")))
                .ToListAsync();

            NormalizePoiThumbnails(pois);
            return Ok(pois);
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

        [HttpGet("public/{id:int}")]
        public async Task<ActionResult<Poi>> GetPublicPoi(int id)
        {
            var poi = await _context.Pois
                .AsNoTracking()
                .Include(p => p.Poilocalizations)
                .Include(p => p.Menus)
                .FirstOrDefaultAsync(p => p.Poiid == id);

            if (poi == null || !IsActiveStatus(poi.Status))
            {
                return NotFound();
            }

            NormalizePoiThumbnail(poi);

            return Ok(poi);
        }

        [HttpGet("owner")]
        [Authorize(Roles = "Owner")]
        public async Task<ActionResult<IEnumerable<OwnerPoiDto>>> GetOwnerPois()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Forbid();
            }

            var poiIds = await _context.PoiSubmissions
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.Poiid != null)
                .Select(s => s.Poiid!.Value)
                .Distinct()
                .ToListAsync();

            var pois = await _context.Pois
                .AsNoTracking()
                .Where(p => poiIds.Contains(p.Poiid))
                .OrderBy(p => p.Name)
                .Select(p => new OwnerPoiDto
                {
                    Poiid = p.Poiid,
                    Name = p.Name,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    Thumbnail = p.Thumbnail,
                    Status = p.Status
                })
                .ToListAsync();

            return Ok(pois);
        }

        [HttpGet("owner/available")]
        [Authorize(Roles = "Owner")]
        public async Task<ActionResult<IEnumerable<AvailablePoiDto>>> GetAvailableOwnerPois()
        {
            var pois = await _context.Pois
                .AsNoTracking()
                .Where(p => !_context.PoiSubmissions.Any(s => s.Poiid == p.Poiid))
                .OrderBy(p => p.Name)
                .Select(p => new AvailablePoiDto
                {
                    Poiid = p.Poiid,
                    Name = p.Name,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Radius = p.Radius,
                    Thumbnail = p.Thumbnail,
                    Status = p.Status
                })
                .ToListAsync();

            return Ok(pois);
        }

        [HttpPost("owner")]
        [Authorize(Roles = "Owner")]
        public async Task<ActionResult<OwnerPoiDto>> CreateOwnerPoi([FromBody] OwnerPoiCreateDto request)
        {
            if (request == null)
            {
                return BadRequest("Tên quán không được để trống.");
            }

            if (!request.Poiid.HasValue && string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Tên quán không được để trống.");
            }

            if (!request.Poiid.HasValue && request.Latitude is < -90 or > 90)
            {
                return BadRequest("Vĩ độ không hợp lệ. Giá trị phải trong khoảng -90 đến 90.");
            }

            if (!request.Poiid.HasValue && request.Longitude is < -180 or > 180)
            {
                return BadRequest("Kinh độ không hợp lệ. Giá trị phải trong khoảng -180 đến 180.");
            }

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Forbid();
            }

            if (request.Poiid.HasValue)
            {
                var poi = await _context.Pois.FirstOrDefaultAsync(p => p.Poiid == request.Poiid.Value);
                if (poi == null)
                {
                    return BadRequest("Không tìm thấy quán để nhận.");
                }

                if (!IsActiveStatus(poi.Status))
                {
                    return BadRequest("Quán đang tạm ẩn, không thể nhận.");
                }

                var isAssigned = await _context.PoiSubmissions
                    .AsNoTracking()
                    .AnyAsync(s => s.Poiid == request.Poiid.Value);

                if (isAssigned)
                {
                    return BadRequest("Quán đã có chủ.");
                }

                var submission = new PoiSubmission
                {
                    Poiid = poi.Poiid,
                    UserId = userId,
                    Status = 1
                };

                await _context.PoiSubmissions.AddAsync(submission);
                await _context.SaveChangesAsync();

                return Ok(new OwnerPoiDto
                {
                    Poiid = poi.Poiid,
                    Name = poi.Name,
                    Latitude = poi.Latitude,
                    Longitude = poi.Longitude,
                    Radius = poi.Radius,
                    Thumbnail = poi.Thumbnail,
                    Status = poi.Status
                });
            }

            var newPoi = new Poi
            {
                Name = request.Name.Trim(),
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Radius = request.Radius ?? 50,
                Thumbnail = request.Thumbnail,
                Status = "Pending",
                CreatedAt = DateTime.Now
            };

            _context.Pois.Add(newPoi);
            await _context.SaveChangesAsync();

            var newSubmission = new PoiSubmission
            {
                Poiid = newPoi.Poiid,
                UserId = userId,
                Status = 1
            };

            await _context.PoiSubmissions.AddAsync(newSubmission);
            await _context.SaveChangesAsync();

            return Ok(new OwnerPoiDto
            {
                Poiid = newPoi.Poiid,
                Name = newPoi.Name,
                Latitude = newPoi.Latitude,
                Longitude = newPoi.Longitude,
                Radius = newPoi.Radius,
                Thumbnail = newPoi.Thumbnail,
                Status = newPoi.Status
            });
        }

        [HttpPut("owner/{id:int}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> UpdateOwnerPoi(int id, [FromBody] OwnerPoiUpdateDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Tên quán không được để trống.");
            }

            if (request.Latitude is < -90 or > 90)
            {
                return BadRequest("Vĩ độ không hợp lệ. Giá trị phải trong khoảng -90 đến 90.");
            }

            if (request.Longitude is < -180 or > 180)
            {
                return BadRequest("Kinh độ không hợp lệ. Giá trị phải trong khoảng -180 đến 180.");
            }

            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Forbid();
            }

            var ownerPoiIds = await _context.PoiSubmissions
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.Poiid != null)
                .Select(s => s.Poiid!.Value)
                .Distinct()
                .ToListAsync();

            if (!ownerPoiIds.Contains(id))
            {
                return Forbid();
            }

            var existingPoi = await _context.Pois.FirstOrDefaultAsync(p => p.Poiid == id);
            if (existingPoi == null)
            {
                return NotFound();
            }

            if (!IsActiveStatus(existingPoi.Status))
            {
                return BadRequest("Quán đang tạm ẩn, không thể chỉnh sửa.");
            }

            existingPoi.Name = request.Name.Trim();
            existingPoi.Latitude = request.Latitude;
            existingPoi.Longitude = request.Longitude;
            existingPoi.Radius = request.Radius;
            existingPoi.Thumbnail = request.Thumbnail;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 3. THÊM MỚI 1 QUÁN
        [HttpPost]
        public async Task<ActionResult<Poi>> PostPoi([FromBody] Poi poi)
        {
            if (poi == null)
            {
                return BadRequest("Dữ liệu gửi lên não hợp lệ.");
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

        private static bool IsActiveStatus(string? status)
            => string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "1", StringComparison.OrdinalIgnoreCase);

        public class OwnerPoiDto
        {
            public int Poiid { get; set; }
            public string Name { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int? Radius { get; set; }
            public string? Thumbnail { get; set; }
            public string? Status { get; set; }
        }

        public class AvailablePoiDto
        {
            public int Poiid { get; set; }
            public string Name { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int? Radius { get; set; }
            public string? Thumbnail { get; set; }
            public string? Status { get; set; }
        }

        public class OwnerPoiCreateDto
        {
            public int? Poiid { get; set; }
            public string Name { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int? Radius { get; set; }
            public string? Thumbnail { get; set; }
        }

        public class OwnerPoiUpdateDto
        {
            public string Name { get; set; } = string.Empty;
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int? Radius { get; set; }
            public string? Thumbnail { get; set; }
        }
    }
}