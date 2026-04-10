using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers
{
    [Route("api/analytics")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly VinhKhanhAudioGuideContext _context;

        public AnalyticsController(VinhKhanhAudioGuideContext context)
        {
            _context = context;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<AnalyticsDashboardDto>> GetDashboardAsync()
        {
            var totalPois = await _context.Pois.CountAsync();
            var activePois = await _context.Pois.CountAsync(p => p.Status == "Active" || p.Status == "active" || p.Status == "1");
            var pendingPois = await _context.Pois.CountAsync(p => p.Status == "Pending" || p.Status == "pending");
            var inactivePois = Math.Max(0, totalPois - activePois - pendingPois);

            var totalMenus = await _context.Menus.CountAsync();
            var totalVisits = await _context.VisitLogs.CountAsync();
            var pendingSubmissions = await _context.PoiSubmissions.CountAsync(s => s.Status == null || s.Status == 0);

            var today = DateTime.Today;
            var fromDate = today.AddDays(-6);

            var visitByDate = await _context.VisitLogs
                .AsNoTracking()
                .Where(v => v.VisitTime.HasValue && v.VisitTime.Value.Date >= fromDate)
                .GroupBy(v => v.VisitTime!.Value.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var visitsTrend = Enumerable.Range(0, 7)
                .Select(offset => fromDate.AddDays(offset))
                .Select(day => new VisitsTrendItemDto
                {
                    Date = day,
                    TotalVisits = visitByDate.FirstOrDefault(v => v.Date == day)?.Count ?? 0
                })
                .ToList();

            var topPois = await _context.VisitLogs
                .AsNoTracking()
                .Where(v => v.Poiid.HasValue)
                .GroupBy(v => new
                {
                    PoiId = v.Poiid!.Value,
                    PoiName = v.Poi != null ? v.Poi.Name : "Không xác định"
                })
                .Select(g => new TopPoiDto
                {
                    PoiId = g.Key.PoiId,
                    PoiName = g.Key.PoiName,
                    TotalVisits = g.Count()
                })
                .OrderByDescending(x => x.TotalVisits)
                .Take(5)
                .ToListAsync();

            var localizedPois = await _context.Pois
                .AsNoTracking()
                .CountAsync(p => p.Poilocalizations.Any());

            var poisWithoutMenus = await _context.Pois
                .AsNoTracking()
                .CountAsync(p => !p.Menus.Any());

            var poisWithoutThumbnail = await _context.Pois
                .AsNoTracking()
                .CountAsync(p => p.Thumbnail == null || p.Thumbnail == "");

            var weakPois = await _context.Pois
                .AsNoTracking()
                .Select(p => new DataQualityPoiDto
                {
                    PoiId = p.Poiid,
                    PoiName = p.Name,
                    HasMenu = p.Menus.Any(),
                    HasThumbnail = p.Thumbnail != null && p.Thumbnail != ""
                })
                .Where(x => !x.HasMenu || !x.HasThumbnail)
                .OrderBy(x => x.HasMenu)
                .ThenBy(x => x.HasThumbnail)
                .ThenBy(x => x.PoiName)
                .Take(10)
                .ToListAsync();

            var dashboard = new AnalyticsDashboardDto
            {
                TotalPois = totalPois,
                ActivePois = activePois,
                PendingPois = pendingPois,
                InactivePois = inactivePois,
                TotalMenus = totalMenus,
                TotalVisits = totalVisits,
                PendingSubmissions = pendingSubmissions,
                LocalizedPois = localizedPois,
                PoisWithoutMenus = poisWithoutMenus,
                PoisWithoutThumbnail = poisWithoutThumbnail,
                VisitsTrend = visitsTrend,
                TopPois = topPois,
                WeakPois = weakPois
            };

            return Ok(dashboard);
        }

        [HttpGet("owner-dashboard")]
        [Authorize(Roles = "Owner")]
        public async Task<ActionResult<OwnerDashboardStatsDto>> GetOwnerDashboardAsync()
        {
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

            if (ownerPoiIds.Count == 0)
            {
                return Ok(new OwnerDashboardStatsDto());
            }

            var activeMenus = await _context.Menus
                .AsNoTracking()
                .CountAsync(m => m.Poiid.HasValue && ownerPoiIds.Contains(m.Poiid.Value));

            var today = DateTime.Today;
            var todayVisits = await _context.VisitLogs
                .AsNoTracking()
                .CountAsync(v =>
                    v.Poiid.HasValue
                    && ownerPoiIds.Contains(v.Poiid.Value)
                    && v.VisitTime.HasValue
                    && v.VisitTime.Value.Date == today);

            return Ok(new OwnerDashboardStatsDto
            {
                TotalPois = ownerPoiIds.Count,
                ActiveMenus = activeMenus,
                TodayVisits = todayVisits
            });
        }

        public class AnalyticsDashboardDto
        {
            public int TotalPois { get; set; }
            public int ActivePois { get; set; }
            public int PendingPois { get; set; }
            public int InactivePois { get; set; }
            public int TotalMenus { get; set; }
            public int TotalVisits { get; set; }
            public int PendingSubmissions { get; set; }
            public int LocalizedPois { get; set; }
            public int PoisWithoutMenus { get; set; }
            public int PoisWithoutThumbnail { get; set; }
            public List<VisitsTrendItemDto> VisitsTrend { get; set; } = new();
            public List<TopPoiDto> TopPois { get; set; } = new();
            public List<DataQualityPoiDto> WeakPois { get; set; } = new();
        }

        public class VisitsTrendItemDto
        {
            public DateTime Date { get; set; }
            public int TotalVisits { get; set; }
        }

        public class TopPoiDto
        {
            public int PoiId { get; set; }
            public string PoiName { get; set; } = string.Empty;
            public int TotalVisits { get; set; }
        }

        public class DataQualityPoiDto
        {
            public int PoiId { get; set; }
            public string PoiName { get; set; } = string.Empty;
            public bool HasMenu { get; set; }
            public bool HasThumbnail { get; set; }
        }

        public class OwnerDashboardStatsDto
        {
            public int TotalPois { get; set; }
            public int ActiveMenus { get; set; }
            public int TodayVisits { get; set; }
        }

    }
}
