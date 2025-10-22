using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    }

    public class ChangeRoleDto
    {
        public string Role { get; set; } = "";
    }
}
