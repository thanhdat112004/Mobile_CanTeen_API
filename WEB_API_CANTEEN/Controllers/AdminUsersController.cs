using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "ADMIN")]
    public class AdminUsersController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public AdminUsersController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // GET /api/admin/users?q=&role=&isActive=&page=1&pageSize=20
        [HttpGet]
        public IActionResult List([FromQuery] string? q, [FromQuery] string? role,
                                  [FromQuery] bool? isActive, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = page <= 0 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var users = _ctx.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim();
                users = users.Where(u => u.Username.Contains(kw) || u.Name.Contains(kw) || (u.Phone ?? "").Contains(kw));
            }
            if (!string.IsNullOrWhiteSpace(role))
            {
                var r = role.Trim().ToUpperInvariant();
                users = users.Where(u => u.Role == r);
            }
            if (isActive.HasValue) users = users.Where(u => u.IsActive == isActive.Value);

            var total = users.Count();
            var data = users.OrderBy(u => u.Username)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .Select(u => new
                            {
                                u.Id,
                                u.Name,
                                u.Username,
                                u.Phone,
                                u.Role,
                                u.IsActive,
                                u.CreatedAt
                            }).ToList();

            return Ok(new { page, pageSize, total, data });
        }

        // GET /api/admin/users/{id}
        [HttpGet("{id:long}")]
        public IActionResult GetById(long id)
        {
            var u = _ctx.Users.FirstOrDefault(x => x.Id == id);
            if (u == null) return NotFound();

            return Ok(new
            {
                u.Id,
                u.Username,
                Name = u.Name,
                u.Email,
                u.Phone,
                u.Mssv,
                u.Class,
                u.Allergies,
                u.Preferences,
                u.Role,
                u.IsActive,
                u.CreatedAt
            });
        }

        // POST /api/admin/users
        [HttpPost]
        public IActionResult Create([FromBody] AdminCreateUserDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var username = dto.Username.Trim();
            var role = (dto.Role ?? "USER").Trim().ToUpperInvariant();
            if (role is not ("USER" or "STAFF" or "ADMIN"))
                return BadRequest("Role không hợp lệ.");

            if (_ctx.Users.Any(u => u.Username == username))
                return Conflict("Username đã tồn tại.");

            if (!string.IsNullOrWhiteSpace(dto.Email) && _ctx.Users.Any(u => u.Email == dto.Email))
                return Conflict("Email đã tồn tại.");

            var user = new User
            {
                Username = username,
                Name = string.IsNullOrWhiteSpace(dto.FullName) ? username : dto.FullName!.Trim(),
                Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email!.Trim(),
                PasswordHash = string.IsNullOrWhiteSpace(dto.Password) ? null : Sha256(dto.Password!),
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone!.Trim(),
                Mssv = string.IsNullOrWhiteSpace(dto.Mssv) ? null : dto.Mssv!.Trim(),
                Class = string.IsNullOrWhiteSpace(dto.Class) ? null : dto.Class!.Trim(),
                Allergies = string.IsNullOrWhiteSpace(dto.Allergies) ? null : dto.Allergies!.Trim(),
                Preferences = string.IsNullOrWhiteSpace(dto.Preferences) ? null : dto.Preferences!.Trim(),
                Role = role,
                IsActive = dto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow
            };

            _ctx.Users.Add(user);
            _ctx.SaveChanges();

            return CreatedAtAction(nameof(GetById), new { id = user.Id }, new { user.Id });
        }

        // PATCH /api/admin/users/{id}
        [HttpPatch("{id:long}")]
        public IActionResult Update(long id, [FromBody] AdminUpdateUserDto dto)
        {
            var u = _ctx.Users.FirstOrDefault(x => x.Id == id);
            if (u == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Username))
            {
                var newUsername = dto.Username!.Trim();
                if (newUsername != u.Username && _ctx.Users.Any(x => x.Username == newUsername))
                    return Conflict("Username đã tồn tại.");
                u.Username = newUsername;
            }

            if (!string.IsNullOrWhiteSpace(dto.FullName)) u.Name = dto.FullName!.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var newEmail = dto.Email!.Trim();
                if (!string.Equals(newEmail, u.Email, StringComparison.OrdinalIgnoreCase) &&
                    _ctx.Users.Any(x => x.Email == newEmail))
                    return Conflict("Email đã tồn tại.");
                u.Email = newEmail;
            }
            if (!string.IsNullOrWhiteSpace(dto.Phone)) u.Phone = dto.Phone!.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Mssv)) u.Mssv = dto.Mssv!.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Class)) u.Class = dto.Class!.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Allergies)) u.Allergies = dto.Allergies!.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Preferences)) u.Preferences = dto.Preferences!.Trim();

            if (!string.IsNullOrWhiteSpace(dto.Role))
            {
                var role = dto.Role!.Trim().ToUpperInvariant();
                if (role is not ("USER" or "STAFF" or "ADMIN"))
                    return BadRequest("Role không hợp lệ.");
                u.Role = role;
            }

            if (dto.IsActive.HasValue) u.IsActive = dto.IsActive.Value;

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                u.PasswordHash = Sha256(dto.NewPassword!);
            }

            _ctx.SaveChanges();
            return NoContent();
        }

        // DELETE /api/admin/users/{id}
        [HttpDelete("{id:long}")]
        public IActionResult Delete(long id)
        {
            var u = _ctx.Users.FirstOrDefault(x => x.Id == id);
            if (u == null) return NotFound();

            // Không cho xóa khi có dữ liệu liên quan (đơn hàng, điểm...)
            var hasRelated = _ctx.Orders.Any(o => o.UserId == id)
                              || _ctx.PaymentTransactions.Any(p => p.ActorId == id)
                              || _ctx.PointsLedgers.Any(p => p.UserId == id)
                              || _ctx.UserNotifications.Any(n => n.UserId == id);
            if (hasRelated)
                return BadRequest("Không thể xóa người dùng do có dữ liệu liên quan. Vui lòng vô hiệu hóa thay vì xóa.");

            _ctx.Users.Remove(u);
            _ctx.SaveChanges();
            return NoContent();
        }

        // PATCH /api/admin/users/{id}/toggle-active
        [HttpPatch("{id:long}/toggle-active")]
        public IActionResult ToggleActive(long id)
        {
            var u = _ctx.Users.Find(id);
            if (u == null) return NotFound();
            u.IsActive = !u.IsActive;
            _ctx.SaveChanges();
            return Ok(new { u.Id, u.IsActive });
        }

        // PATCH /api/admin/users/{id}/role
        // body: { "role": "USER|STAFF|ADMIN" }
        [HttpPatch("{id:long}/role")]
        public IActionResult ChangeRole(long id, [FromBody] ChangeRoleDto dto)
        {
            var u = _ctx.Users.Find(id);
            if (u == null) return NotFound();

            var role = (dto.Role ?? "").Trim().ToUpperInvariant();
            if (role is not ("USER" or "STAFF" or "ADMIN"))
                return BadRequest("Role không hợp lệ.");

            u.Role = role;
            _ctx.SaveChanges();
            return Ok(new { u.Id, u.Role });
        }

        private static string Sha256(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    public class ChangeRoleDto
    {
        public string Role { get; set; } = "";
    }

    public class AdminCreateUserDto
    {
        [Required, MinLength(3)] public string Username { get; set; } = "";
        [MinLength(1)] public string? FullName { get; set; }
        [EmailAddress] public string? Email { get; set; }
        [MinLength(6)] public string? Password { get; set; }
        public string? Phone { get; set; }
        public string? Mssv { get; set; }
        public string? Class { get; set; }
        public string? Allergies { get; set; }
        public string? Preferences { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
    }

    public class AdminUpdateUserDto
    {
        public string? Username { get; set; }
        public string? FullName { get; set; }
        [EmailAddress] public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Mssv { get; set; }
        public string? Class { get; set; }
        public string? Allergies { get; set; }
        public string? Preferences { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
        // Admin đặt lại mật khẩu
        [MinLength(6)] public string? NewPassword { get; set; }
    }
}
