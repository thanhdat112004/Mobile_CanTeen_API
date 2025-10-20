// AuthController: Đăng ký, Đăng nhập, Quên/Đặt lại mật khẩu (in-memory), Logout, phát hành JWT
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/auth
    public class AuthController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IConfiguration _cfg;

        // Lưu OTP reset mật khẩu trong bộ nhớ tạm: key = username
        private static readonly ConcurrentDictionary<string, (string Code, DateTime ExpireAt)> _resetStore = new();

        public AuthController(SmartCanteenDbContext ctx, IConfiguration cfg)
        {
            _ctx = ctx;
            _cfg = cfg;
        }

        // POST /api/auth/register
        // Tạo tài khoản mới -> Role mặc định USER. Lưu đầy đủ Phone, Mssv, Class, Allergies, Preferences nếu gửi lên.
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (_ctx.Users.Any(u => u.Username == req.Username))
                return Conflict("Username đã tồn tại");

            var user = new User
            {
                Username = req.Username,
                Name = string.IsNullOrWhiteSpace(req.FullName) ? req.Username : req.FullName!,
                PasswordHash = Sha256(req.Password),
                Phone = req.Phone,
                Mssv = req.Mssv,
                Class = req.Class,
                Allergies = req.Allergies,
                Preferences = req.Preferences,
                Role = "USER",            // 🔒 mặc định USER
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _ctx.Users.Add(user);
            _ctx.SaveChanges();

            var token = GenerateJwt(user);
            return Ok(new { message = "Đăng ký thành công", token, role = user.Role, username = user.Username });
        }

        // POST /api/auth/login
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            var user = _ctx.Users.FirstOrDefault(u => u.Username == req.Username);
            if (user == null) return Unauthorized("Sai tài khoản hoặc mật khẩu");

            // Hỗ trợ dữ liệu cũ nếu PasswordHash đang là plain
            var ok = user.PasswordHash == Sha256(req.Password) || user.PasswordHash == req.Password;
            if (!ok) return Unauthorized("Sai tài khoản hoặc mật khẩu");

            var token = GenerateJwt(user);
            return Ok(new { token, role = user.Role, username = user.Username });
        }

        // POST /api/auth/forgot-password
        // Sinh mã 6 ký tự, lưu in-memory 10 phút (đồ án không cần SMTP)
        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest req)
        {
            var user = _ctx.Users.FirstOrDefault(u => u.Username == req.Username);
            if (user == null) return NotFound("Không tìm thấy người dùng");

            var code = Guid.NewGuid().ToString("N")[..6].ToUpper();
            var expire = DateTime.UtcNow.AddMinutes(10);
            _resetStore.AddOrUpdate(user.Username, (code, expire), (_, __) => (code, expire));

            return Ok(new { message = "Đã tạo mã khôi phục", username = user.Username, resetCode = code, expireAt = expire });
        }

        // POST /api/auth/reset-password
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest req)
        {
            var user = _ctx.Users.FirstOrDefault(u => u.Username == req.Username);
            if (user == null) return NotFound("Không tìm thấy người dùng");

            if (!_resetStore.TryGetValue(user.Username, out var e))
                return BadRequest("Chưa yêu cầu khôi phục");
            if (e.ExpireAt < DateTime.UtcNow) return BadRequest("Mã đã hết hạn");
            if (!string.Equals(e.Code, req.ResetCode, StringComparison.OrdinalIgnoreCase))
                return BadRequest("Mã không đúng");

            user.PasswordHash = Sha256(req.NewPassword);
            _ctx.SaveChanges();

            _resetStore.TryRemove(user.Username, out _);
            return Ok(new { message = "Đặt lại mật khẩu thành công" });
        }

        // POST /api/auth/logout
        // JWT là stateless -> client chỉ cần xoá token. Endpoint này trả OK cho luồng UI.
        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout() => Ok(new { message = "Logged out. Please remove token on client." });

        // ================= Helpers =================

        private string GenerateJwt(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? "USER")
            };

            // Hỗ trợ key dạng base64:... hoặc chuỗi thường, và kiểm tra độ dài >= 32 bytes
            var keyConfig = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
            byte[] keyBytes = keyConfig.StartsWith("base64:", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(keyConfig["base64:".Length..])
                : Encoding.UTF8.GetBytes(keyConfig);

            if (keyBytes.Length < 32)
                throw new InvalidOperationException("Jwt:Key must be at least 32 bytes (256 bits) for HS256.");

            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _cfg["Jwt:Issuer"],
                audience: _cfg["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:ExpireMinutes"] ?? "120")),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string Sha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    // ================= DTOs =================

    public class RegisterRequest
    {
        [Required, MinLength(3)] public string Username { get; set; } = "";
        [Required, MinLength(6)] public string Password { get; set; } = "";
        [MaxLength(120)] public string? FullName { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        [MaxLength(30)] public string? Mssv { get; set; }
        [MaxLength(30)] public string? Class { get; set; }
        [MaxLength(255)] public string? Allergies { get; set; }
        [MaxLength(255)] public string? Preferences { get; set; }
    }

    public class LoginRequest
    {
        [Required] public string Username { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }

    public class ForgotPasswordRequest
    {
        [Required] public string Username { get; set; } = "";
    }

    public class ResetPasswordRequest
    {
        [Required] public string Username { get; set; } = "";
        [Required] public string ResetCode { get; set; } = "";
        [Required, MinLength(6)] public string NewPassword { get; set; } = "";
    }
}
