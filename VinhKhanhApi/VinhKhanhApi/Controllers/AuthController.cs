using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using VinhKhanhApi.Models; // Đảm bảo đúng namespace Models của bạn
using Microsoft.EntityFrameworkCore; // Thêm dòng này để dùng Entity Framework

namespace VinhKhanhApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        // Khai báo biến để gọi Database
        private readonly VinhKhanhAudioGuideContext _context;

        public AuthController(VinhKhanhAudioGuideContext context)
        {
            _context = context;
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        [HttpPost("login-admin")]
        public async Task<IActionResult> LoginAdmin([FromBody] LoginRequest request)
        {
            // 1. Chui xuống Database, tìm user có Username và Password khớp với người dùng nhập
            // (Hiện tại mình check text trần vì trong SQL bạn đang lưu '123456', sau này mình sẽ mã hóa sau cho chuẩn Enterprise)
            var user = await _context.AdminUsers
                .FirstOrDefaultAsync(u => u.UserName == request.Username && u.PasswordHash == request.Password);

            // 2. Nếu tìm không ra -> Báo lỗi
            if (user == null)
            {
                return Unauthorized("Sai tài khoản hoặc mật khẩu");
            }

            if (user.RoleId != 1)
            {
                return Unauthorized("Tài khoản không có quyền truy cập");
            }

            // 3. Nếu tìm thấy, xem RoleID của họ là gì (1)
            string roleName = "Admin";

            // 4. Phát Token
            var token = GenerateJwtToken(user.UserName, roleName, user.UserId);

            return Ok(new
            {
                Token = token,
                Role = roleName,
                FullName = user.FullName, // Trả về thêm tên thật để mốt hiển thị lên góc màn hình Web
                Message = "Đăng nhập thành công"
            });
        }

        [HttpPost("login-app")]
        public async Task<IActionResult> LoginApp([FromBody] LoginRequest request)
        {
            var user = await _context.AdminUsers
                .FirstOrDefaultAsync(u => u.UserName == request.Username && u.PasswordHash == request.Password);

            if (user == null)
            {
                return Unauthorized("Sai tài khoản hoặc mật khẩu");
            }

            if (user.RoleId != 2 && user.RoleId != 3)
            {
                return Unauthorized("Tài khoản không có quyền truy cập");
            }

            string roleName = user.RoleId == 2 ? "Role2" : "Role3";

            var token = GenerateJwtToken(user.UserName, roleName, user.UserId);

            return Ok(new
            {
                Token = token,
                Role = roleName,
                FullName = user.FullName,
                Message = "Đăng nhập thành công"
            });
        }

        [HttpPost("login-web")]
        public async Task<IActionResult> LoginWeb([FromBody] LoginRequest request)
        {
            var user = await _context.AdminUsers
                .FirstOrDefaultAsync(u => u.UserName == request.Username && u.PasswordHash == request.Password);

            if (user == null)
            {
                return Unauthorized("Sai tài khoản hoặc mật khẩu");
            }

            if (user.RoleId != 1 && user.RoleId != 2)
            {
                return Unauthorized("Tài khoản không có quyền truy cập web");
            }

            string roleName = user.RoleId == 1 ? "Admin" : "Owner";

            var token = GenerateJwtToken(user.UserName, roleName, user.UserId);

            return Ok(new
            {
                Token = token,
                Role = roleName,
                FullName = user.FullName,
                Message = "Đăng nhập thành công"
            });
        }

        // Hàm tạo Token (Giữ nguyên như cũ)
        private string GenerateJwtToken(string username, string role, int userId)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("VinhKhanhFood_Super_Secret_Key_12345!!!"));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim("UserId", userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: "VinhKhanhApi",
                audience: "VinhKhanhApp",
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}