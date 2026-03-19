using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VisitLogsController : ControllerBase
    {
        private readonly VinhKhanhAudioGuideContext _context;

        public VisitLogsController(VinhKhanhAudioGuideContext context)
        {
            _context = context;
        }

        // API này để App Mobile gửi dữ liệu về khi khách đến gần quán
        [HttpPost]
        public async Task<ActionResult<VisitLog>> PostVisitLog(VisitLog visitLog)
        {
            visitLog.VisitTime = DateTime.Now;
            _context.VisitLogs.Add(visitLog);
            await _context.Set<VisitLog>().AddAsync(visitLog); // Dùng cho EF Core 8
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ghi nhận lượt ghé thăm thành công!" });
        }
    }
}