// UsersController: Hồ sơ cá nhân (me), cập nhật thông tin, đổi mật khẩu
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/users
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public UsersController(SmartCanteenDbContext ctx) => _ctx = ctx;

        private long GetUserId()
        {
            var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(id)) throw new UnauthorizedAccessException();
            return long.Parse(id);
        }

        // GET /api/users/me
        [HttpGet("me")]
        public IActionResult GetMe()
        {
            var uid = GetUserId();
            var u = _ctx.Users.FirstOrDefault(x => x.Id == uid);
            if (u == null) return NotFound();

            return Ok(new UserProfileDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.Name,
                Phone = u.Phone,
                Mssv = u.Mssv,
                Class = u.Class,
                Allergies = u.Allergies,
                Preferences = u.Preferences,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            });
        }

        // PATCH /api/users/me
        // Cho phép sửa: FullName/Phone/Mssv/Class/Allergies/Preferences
        [HttpPatch("me")]
        public IActionResult UpdateMe([FromBody] UpdateProfileRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var uid = GetUserId();
            var u = _ctx.Users.FirstOrDefault(x => x.Id == uid);
            if (u == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.FullName)) u.Name = req.FullName!;
            if (!string.IsNullOrWhiteSpace(req.Phone)) u.Phone = req.Phone!;
            if (!string.IsNullOrWhiteSpace(req.Mssv)) u.Mssv = req.Mssv!;
            if (!string.IsNullOrWhiteSpace(req.Class)) u.Class = req.Class!;
            if (!string.IsNullOrWhiteSpace(req.Allergies)) u.Allergies = req.Allergies!;
            if (!string.IsNullOrWhiteSpace(req.Preferences)) u.Preferences = req.Preferences!;

            _ctx.SaveChanges();
            return NoContent();
        }

        // PATCH /api/users/me/password
        [HttpPatch("me/password")]
        public IActionResult ChangePassword([FromBody] ChangePasswordRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var uid = GetUserId();
            var u = _ctx.Users.FirstOrDefault(x => x.Id == uid);
            if (u == null) return NotFound();

            var oldOk = u.PasswordHash == Sha256(req.OldPassword) || u.PasswordHash == req.OldPassword;
            if (!oldOk) return BadRequest("Mật khẩu cũ không đúng");

            u.PasswordHash = Sha256(req.NewPassword);
            _ctx.SaveChanges();

            return NoContent();
        }

        // ===== Helpers =====
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

    public class UserProfileDto
    {
        public long Id { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Mssv { get; set; }
        public string? Class { get; set; }
        public string? Allergies { get; set; }
        public string? Preferences { get; set; }
        public string Role { get; set; } = "USER";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateProfileRequest
    {
        [MaxLength(120)] public string? FullName { get; set; }
        [MaxLength(20)] public string? Phone { get; set; }
        [MaxLength(30)] public string? Mssv { get; set; }
        [MaxLength(30)] public string? Class { get; set; }
        [MaxLength(255)] public string? Allergies { get; set; }
        [MaxLength(255)] public string? Preferences { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required, MinLength(6)] public string OldPassword { get; set; } = "";
        [Required, MinLength(6)] public string NewPassword { get; set; } = "";
    }
}
