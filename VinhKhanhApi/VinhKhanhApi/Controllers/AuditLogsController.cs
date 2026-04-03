using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers
{
    [Route("api/audit-logs")]
    [ApiController]
    public class AuditLogsController : ControllerBase
    {
        private readonly VinhKhanhAudioGuideContext _context;

        public AuditLogsController(VinhKhanhAudioGuideContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<AuditLogPagedResultDto>> GetAuditLogsAsync(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? userId,
            [FromQuery] string? keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var query = BuildFilteredQuery(from, to, userId, keyword);

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page > totalPages)
            {
                page = totalPages;
            }

            var items = await query
                .OrderByDescending(a => a.Timestamp)
                .ThenByDescending(a => a.LogId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogItemDto
                {
                    LogId = a.LogId,
                    UserId = a.UserId,
                    UserName = a.User != null ? a.User.UserName : null,
                    FullName = a.User != null ? a.User.FullName : null,
                    Action = a.Action,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();

            var uniqueUsers = await query
                .Where(a => a.UserId.HasValue)
                .Select(a => a.UserId)
                .Distinct()
                .CountAsync();

            var topAction = await query
                .Where(a => a.Action != null && a.Action != "")
                .GroupBy(a => a.Action)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync();

            var latestTimestamp = await query.MaxAsync(a => a.Timestamp);

            return Ok(new AuditLogPagedResultDto
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Items = items,
                Summary = new AuditLogSummaryDto
                {
                    UniqueUsers = uniqueUsers,
                    TopAction = topAction,
                    LatestTimestamp = latestTimestamp
                }
            });
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<AuditLogUserOptionDto>>> GetUsersAsync()
        {
            var users = await _context.AdminUsers
                .AsNoTracking()
                .Where(u => u.AuditLogs.Any())
                .OrderBy(u => u.FullName ?? u.UserName)
                .Select(u => new AuditLogUserOptionDto
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    FullName = u.FullName
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("export-csv")]
        public async Task<IActionResult> ExportCsvAsync(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int? userId,
            [FromQuery] string? keyword)
        {
            var items = await BuildFilteredQuery(from, to, userId, keyword)
                .OrderByDescending(a => a.Timestamp)
                .ThenByDescending(a => a.LogId)
                .Select(a => new AuditLogItemDto
                {
                    LogId = a.LogId,
                    UserId = a.UserId,
                    UserName = a.User != null ? a.User.UserName : null,
                    FullName = a.User != null ? a.User.FullName : null,
                    Action = a.Action,
                    Timestamp = a.Timestamp
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("LogId,Timestamp,UserId,UserName,FullName,Action");
            foreach (var item in items)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    item.LogId.ToString(),
                    EscapeCsv(item.Timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty),
                    EscapeCsv(item.UserId?.ToString() ?? string.Empty),
                    EscapeCsv(item.UserName ?? string.Empty),
                    EscapeCsv(item.FullName ?? string.Empty),
                    EscapeCsv(item.Action ?? string.Empty)
                }));
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"audit-logs-{DateTime.Now:yyyyMMddHHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        private IQueryable<AuditLog> BuildFilteredQuery(DateTime? from, DateTime? to, int? userId, string? keyword)
        {
            var query = _context.AuditLogs
                .AsNoTracking()
                .AsQueryable();

            if (from.HasValue)
            {
                var fromDate = from.Value.Date;
                query = query.Where(a => a.Timestamp.HasValue && a.Timestamp.Value >= fromDate);
            }

            if (to.HasValue)
            {
                var toExclusive = to.Value.Date.AddDays(1);
                query = query.Where(a => a.Timestamp.HasValue && a.Timestamp.Value < toExclusive);
            }

            if (userId.HasValue)
            {
                query = query.Where(a => a.UserId == userId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var search = keyword.Trim();
                query = query.Where(a => a.Action != null && EF.Functions.Like(a.Action, $"%{search}%"));
            }

            return query;
        }

        private static string EscapeCsv(string value)
        {
            if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        public class AuditLogPagedResultDto
        {
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
            public List<AuditLogItemDto> Items { get; set; } = new();
            public AuditLogSummaryDto Summary { get; set; } = new();
        }

        public class AuditLogItemDto
        {
            public int LogId { get; set; }
            public int? UserId { get; set; }
            public string? UserName { get; set; }
            public string? FullName { get; set; }
            public string? Action { get; set; }
            public DateTime? Timestamp { get; set; }
        }

        public class AuditLogSummaryDto
        {
            public int UniqueUsers { get; set; }
            public string? TopAction { get; set; }
            public DateTime? LatestTimestamp { get; set; }
        }

        public class AuditLogUserOptionDto
        {
            public int UserId { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string? FullName { get; set; }
        }
    }
}
