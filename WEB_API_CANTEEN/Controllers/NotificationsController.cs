// NotificationsController: danh sách thông báo & đánh dấu đã đọc
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/notifications
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public NotificationsController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // GET /api/notifications
        [HttpGet]
        public IActionResult List()
        {
            var uidStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(uidStr)) return Unauthorized();
            var uid = long.Parse(uidStr);

            var data = _ctx.UserNotifications
                .Where(n => n.UserId == uid)
                .OrderByDescending(n => n.CreatedAt)
                .ToList();

            return Ok(data);
        }

        // PATCH /api/notifications/{id}/read
        [HttpPatch("{id}/read")]
        public IActionResult Read(long id)
        {
            var uid = long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var n = _ctx.UserNotifications.FirstOrDefault(x => x.Id == id && x.UserId == uid);
            if (n == null) return NotFound();
            n.IsRead = true;
            _ctx.SaveChanges();
            return NoContent();
        }
    }
}
