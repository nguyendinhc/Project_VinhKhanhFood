using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VinhKhanhApi.Models;

namespace VinhKhanhApi.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private const int AdminRoleId = 1;
        private readonly VinhKhanhAudioGuideContext _context;

        public UsersController(VinhKhanhAudioGuideContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<UserPagedResultDto>> GetUsersAsync(
            [FromQuery] string? keyword,
            [FromQuery] int? roleId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var query = BuildFilteredQuery(keyword, roleId);

            var totalCount = await query.CountAsync();
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page > totalPages)
            {
                page = totalPages;
            }

            var items = await query
                .OrderBy(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserItemDto
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    FullName = u.FullName,
                    Email = u.Email,
                    RoleId = u.RoleId,
                    RoleName = u.Role != null ? u.Role.RoleName : null
                })
                .ToListAsync();

            var roleStats = await query
                .GroupBy(u => u.Role != null ? u.Role.RoleName : null)
                .Select(g => new
                {
                    RoleName = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var summary = new UserSummaryDto
            {
                TotalUsers = totalCount,
                AdminCount = roleStats.Where(x => string.Equals(x.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
                OwnerCount = roleStats.Where(x => string.Equals(x.RoleName, "Owner", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count),
                OtherRoleCount = roleStats.Where(x => !string.Equals(x.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)
                                                    && !string.Equals(x.RoleName, "Owner", StringComparison.OrdinalIgnoreCase))
                                         .Sum(x => x.Count)
            };

            return Ok(new UserPagedResultDto
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Items = items,
                Summary = summary
            });
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserItemDto>> GetUserByIdAsync(int id)
        {
            var user = await _context.AdminUsers
                .AsNoTracking()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
            {
                return NotFound("Không tìm thấy user.");
            }

            return Ok(new UserItemDto
            {
                UserId = user.UserId,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                RoleId = user.RoleId,
                RoleName = user.Role?.RoleName
            });
        }

        [HttpGet("roles")]
        public async Task<ActionResult<IEnumerable<RoleOptionDto>>> GetRolesAsync()
        {
            var roles = await _context.Roles
                .AsNoTracking()
                .OrderBy(r => r.RoleName)
                .Select(r => new RoleOptionDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName
                })
                .ToListAsync();

            return Ok(roles);
        }

        [HttpPost]
        public async Task<ActionResult<UserItemDto>> CreateUserAsync([FromBody] CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                return BadRequest("Tên đăng nhập không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Mật khẩu không được để trống.");
            }

            var userName = request.UserName.Trim();
            var isDuplicate = await _context.AdminUsers
                .AnyAsync(u => u.UserName == userName);

            if (isDuplicate)
            {
                return Conflict("Tên đăng nhập đã tồn tại.");
            }

            if (request.RoleId.HasValue)
            {
                var roleExists = await _context.Roles.AnyAsync(r => r.RoleId == request.RoleId.Value);
                if (!roleExists)
                {
                    return BadRequest("Role không hợp lệ.");
                }
            }

            var user = new AdminUser
            {
                UserName = userName,
                PasswordHash = request.Password.Trim(),
                FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim(),
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                RoleId = request.RoleId
            };

            _context.AdminUsers.Add(user);
            await _context.SaveChangesAsync();

            await AddAuditLogAsync($"Create user #{user.UserId} ({user.UserName})");

            var roleName = await _context.Roles
                .AsNoTracking()
                .Where(r => r.RoleId == user.RoleId)
                .Select(r => r.RoleName)
                .FirstOrDefaultAsync();

            var result = new UserItemDto
            {
                UserId = user.UserId,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                RoleId = user.RoleId,
                RoleName = roleName
            };

            return CreatedAtAction("GetUserById", new { id = user.UserId }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateUserAsync(int id, [FromBody] UpdateUserRequest request)
        {
            var user = await _context.AdminUsers.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
            {
                return NotFound("Không tìm thấy user.");
            }

            var actorUserId = GetActorUserId();
            if (user.RoleId == AdminRoleId && user.UserId != actorUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Không thể chỉnh sửa tài khoản Admin khác.");
            }

            if (user.UserId == actorUserId && request.RoleId.HasValue && request.RoleId.Value != AdminRoleId)
            {
                return BadRequest("Không thể tự thay đổi role của chính mình.");
            }

            if (request.RoleId.HasValue)
            {
                var roleExists = await _context.Roles.AnyAsync(r => r.RoleId == request.RoleId.Value);
                if (!roleExists)
                {
                    return BadRequest("Role không hợp lệ.");
                }
            }

            user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
            user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            user.RoleId = request.RoleId;

            await _context.SaveChangesAsync();
            await AddAuditLogAsync($"Update user #{user.UserId} ({user.UserName})");

            return NoContent();
        }

        [HttpPut("{id:int}/password")]
        public async Task<IActionResult> ResetPasswordAsync(int id, [FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest("Mật khẩu mới không được để trống.");
            }

            if (request.NewPassword.Trim().Length < 6)
            {
                return BadRequest("Mật khẩu mới phải có ít nhất 6 ký tự.");
            }

            var user = await _context.AdminUsers.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
            {
                return NotFound("Không tìm thấy user.");
            }

            var actorUserId = GetActorUserId();
            if (user.RoleId == AdminRoleId && user.UserId != actorUserId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Không thể đặt lại mật khẩu cho Admin khác.");
            }

            user.PasswordHash = request.NewPassword.Trim();
            await _context.SaveChangesAsync();
            await AddAuditLogAsync($"Reset password for user #{user.UserId} ({user.UserName})");

            return NoContent();
        }

        private IQueryable<AdminUser> BuildFilteredQuery(string? keyword, int? roleId)
        {
            var query = _context.AdminUsers
                .AsNoTracking()
                .Include(u => u.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var search = keyword.Trim();
                query = query.Where(u =>
                    EF.Functions.Like(u.UserName, $"%{search}%") ||
                    (u.FullName != null && EF.Functions.Like(u.FullName, $"%{search}%")) ||
                    (u.Email != null && EF.Functions.Like(u.Email, $"%{search}%")));
            }

            if (roleId.HasValue)
            {
                query = query.Where(u => u.RoleId == roleId.Value);
            }

            return query;
        }

        private async Task AddAuditLogAsync(string action)
        {
            var actorUserId = GetActorUserId();

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = action,
                Timestamp = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        private int? GetActorUserId()
        {
            var userIdValue = User.FindFirstValue("UserId");
            return int.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : null;
        }

        public class UserPagedResultDto
        {
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
            public List<UserItemDto> Items { get; set; } = new();
            public UserSummaryDto Summary { get; set; } = new();
        }

        public class UserSummaryDto
        {
            public int TotalUsers { get; set; }
            public int AdminCount { get; set; }
            public int OwnerCount { get; set; }
            public int OtherRoleCount { get; set; }
        }

        public class UserItemDto
        {
            public int UserId { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public int? RoleId { get; set; }
            public string? RoleName { get; set; }
        }

        public class RoleOptionDto
        {
            public int RoleId { get; set; }
            public string RoleName { get; set; } = string.Empty;
        }

        public class CreateUserRequest
        {
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public int? RoleId { get; set; }
        }

        public class UpdateUserRequest
        {
            public string? FullName { get; set; }
            public string? Email { get; set; }
            public int? RoleId { get; set; }
        }

        public class ResetPasswordRequest
        {
            public string NewPassword { get; set; } = string.Empty;
        }
    }
}
