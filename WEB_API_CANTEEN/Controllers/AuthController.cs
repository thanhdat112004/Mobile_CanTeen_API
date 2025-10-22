// AuthController: Flow OTP Gating cho Đăng ký & Quên mật khẩu + Login bằng email/username
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WEB_API_CANTEEN.Models;
using WEB_API_CANTEEN.Services;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/auth
    public class AuthController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IConfiguration _cfg;
        private readonly IEmailService _email;
        private readonly IOtpService _otp;

        public AuthController(
            SmartCanteenDbContext ctx,
            IConfiguration cfg,
            IEmailService email,
            IOtpService otp)
        {
            _ctx = ctx;
            _cfg = cfg;
            _email = email;
            _otp = otp;
        }

        // =========================
        // 1) ĐĂNG KÝ (Bước 1): Gửi OTP
        // =========================
        // Body: { "email": "a@b.com" }
        [AllowAnonymous]
        [HttpPost("register/request-otp")]
        public async Task<IActionResult> RegisterRequestOtp([FromBody] RegisterRequestOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || !new EmailAddressAttribute().IsValid(dto.Email))
                return BadRequest("Email không hợp lệ.");

            // Email chưa được sử dụng
            var exists = await _ctx.Users.AnyAsync(u => u.Email == dto.Email);
            if (exists) return Conflict("Email đã tồn tại.");

            var key = $"register:{dto.Email}";
            if (!_otp.CanSend(key)) return StatusCode(429, "Vui lòng thử lại sau ít phút.");

            var code = _otp.GenerateAndSave(key);

            var ttl = _cfg["Otp:TtlMinutes"] ?? "5";
            var html = $@"<p>Mã OTP đăng ký Smart Canteen của bạn là:
                          <b style=""font-size:18px"">{code}</b></p>
                          <p>Hiệu lực trong {ttl} phút.</p>";

            await _email.SendAsync(dto.Email, "[Smart Canteen] OTP đăng ký", html);
            return Ok(new { message = "Đã gửi OTP tới email." });
        }

        // =========================
        // 2) ĐĂNG KÝ (Bước 2): Xác nhận OTP + Tạo tài khoản
        // =========================
        // Body:
        // {
        //   "email":"a@b.com","code":"123456",
        //   "username":"sv001","password":"Abc@12345",
        //   "fullName":"Nguyen Van A","phone":"090...", "mssv":"...", "class":"...",
        //   "allergies":"...", "preferences":"..."
        // }
        [AllowAnonymous]
        [HttpPost("register/confirm")]
        public async Task<IActionResult> RegisterConfirm([FromBody] RegisterConfirmDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Verify OTP
            var key = $"register:{dto.Email}";
            if (!_otp.Verify(key, dto.Code)) return BadRequest("OTP không hợp lệ hoặc đã hết hạn.");

            // Kiểm tra trùng username/email
            if (await _ctx.Users.AnyAsync(u => u.Username == dto.Username))
                return Conflict("Username đã tồn tại.");
            if (await _ctx.Users.AnyAsync(u => u.Email == dto.Email))
                return Conflict("Email đã tồn tại.");

            var user = new User
            {
                Username = dto.Username.Trim(),
                Name = string.IsNullOrWhiteSpace(dto.FullName) ? dto.Username.Trim() : dto.FullName!.Trim(),
                Email = dto.Email.Trim(),
                PasswordHash = Sha256(dto.Password),
                Phone = dto.Phone,
                Mssv = dto.Mssv,
                Class = dto.Class,
                Allergies = dto.Allergies,
                Preferences = dto.Preferences,
                Role = "USER",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _ctx.Users.Add(user);
            await _ctx.SaveChangesAsync();

            var token = GenerateJwt(user);
            return Ok(new
            {
                message = "Đăng ký thành công",
                token,
                username = user.Username,
                email = user.Email,
                role = user.Role
            });
        }

        // =========================
        // 3) QUÊN MẬT KHẨU (B1): Gửi OTP
        // =========================
        // Body: { "usernameOrEmail": "sv001 hoặc a@b.com" }
        [AllowAnonymous]
        [HttpPost("reset/request-otp")]
        public async Task<IActionResult> ResetRequestOtp([FromBody] ResetRequestOtpDto dto)
        {
            var id = dto.UsernameOrEmail?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(id)) return BadRequest("Thiếu usernameOrEmail.");

            var user = await _ctx.Users
                .FirstOrDefaultAsync(u => u.Username == id || (u.Email != null && u.Email == id));

            if (user == null) return NotFound("Không tìm thấy tài khoản.");

            // cần có email để nhận OTP
            var email = user.Email;
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Tài khoản chưa có email để nhận OTP.");

            var key = $"reset:{email}";
            if (!_otp.CanSend(key)) return StatusCode(429, "Vui lòng thử lại sau ít phút.");

            var code = _otp.GenerateAndSave(key);
            var ttl = _cfg["Otp:TtlMinutes"] ?? "5";
            var html = $@"<p>Mã OTP đặt lại mật khẩu của bạn là:
                          <b style=""font-size:18px"">{code}</b></p>
                          <p>Hiệu lực trong {ttl} phút.</p>";

            await _email.SendAsync(email!, "[Smart Canteen] OTP đặt lại mật khẩu", html);
            return Ok(new { message = "Đã gửi OTP đặt lại mật khẩu." });
        }

        // =========================
        // 4) QUÊN MẬT KHẨU (B2): Xác nhận OTP + Đặt mật khẩu mới
        // =========================
        // Body: { "usernameOrEmail": "...", "code": "123456", "newPassword": "Abc@12345" }
        [AllowAnonymous]
        [HttpPost("reset/confirm")]
        public async Task<IActionResult> ResetConfirm([FromBody] ResetConfirmDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var id = dto.UsernameOrEmail?.Trim() ?? string.Empty;

            var user = await _ctx.Users
                .FirstOrDefaultAsync(u => u.Username == id || (u.Email != null && u.Email == id));
            if (user == null) return NotFound("Không tìm thấy tài khoản.");

            var email = user.Email ?? id;
            var key = $"reset:{email}";
            if (!_otp.Verify(key, dto.Code)) return BadRequest("OTP không hợp lệ hoặc đã hết hạn.");

            user.PasswordHash = Sha256(dto.NewPassword);
            await _ctx.SaveChangesAsync();

            return Ok(new { message = "Đổi mật khẩu thành công." });
        }

        // =========================
        // 5) ĐĂNG NHẬP (email hoặc username)
        // =========================
        // Body: { "usernameOrEmail": "...", "password": "..." }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var id = dto.UsernameOrEmail?.Trim() ?? string.Empty;

            var user = await _ctx.Users.FirstOrDefaultAsync(u =>
                u.Username == id || (u.Email != null && u.Email == id));
            if (user == null) return Unauthorized("Sai tài khoản hoặc mật khẩu.");

            var ok = user.PasswordHash == Sha256(dto.Password) || user.PasswordHash == dto.Password;
            if (!ok) return Unauthorized("Sai tài khoản hoặc mật khẩu.");

            if (!user.IsActive) return Forbid("Tài khoản đang bị khóa.");

            var token = GenerateJwt(user);
            return Ok(new { token, username = user.Username, email = user.Email, role = user.Role });
        }

        // =========================
        // 6) ĐĂNG XUẤT (client xóa token)
        // =========================
        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout() => Ok(new { message = "Đã đăng xuất (hãy xóa token phía client)." });

        // ============= Helpers =============
        private string GenerateJwt(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? "USER")
            };

            var keyConfig = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
            byte[] keyBytes = keyConfig.StartsWith("base64:", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(keyConfig["base64:".Length..])
                : Encoding.UTF8.GetBytes(keyConfig);
            if (keyBytes.Length < 32)
                throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");

            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _cfg["Jwt:Issuer"],
                audience: _cfg["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:ExpireMinutes"] ?? "120")),
                signingCredentials: creds);

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

    // ============= DTOs =============
    public class RegisterRequestOtpDto
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
    }

    public class RegisterConfirmDto
    {
        [Required, EmailAddress] public string Email { get; set; } = "";
        [Required] public string Code { get; set; } = "";
        [Required, MinLength(3)] public string Username { get; set; } = "";
        [Required, MinLength(6)] public string Password { get; set; } = "";
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Mssv { get; set; }
        public string? Class { get; set; }
        public string? Allergies { get; set; }
        public string? Preferences { get; set; }
    }

    public class ResetRequestOtpDto
    {
        [Required] public string UsernameOrEmail { get; set; } = "";
    }

    public class ResetConfirmDto
    {
        [Required] public string UsernameOrEmail { get; set; } = "";
        [Required] public string Code { get; set; } = "";
        [Required, MinLength(6)] public string NewPassword { get; set; } = "";
    }

    public class LoginDto
    {
        [Required] public string UsernameOrEmail { get; set; } = "";
        [Required] public string Password { get; set; } = "";
    }
}
