using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public NotificationsController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // GET /api/notifications?onlyUnread=false&page=1&pageSize=20
        [HttpGet]
        public IActionResult List([FromQuery] bool onlyUnread = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var uid = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            page = page <= 0 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = _ctx.UserNotifications.Where(n => n.UserId == uid);
            if (onlyUnread) q = q.Where(n => !n.IsRead);

            var total = q.Count();
            var data = q.OrderByDescending(n => n.CreatedAt)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(n => new {
                            n.Id,
                            n.Title,
                            n.Body,
                            n.Type,
                            n.ReferenceId,
                            n.IsRead,
                            n.CreatedAt
                        }).ToList();

            return Ok(new { page, pageSize, total, data });
        }

        // GET /api/notifications/unread-count
        [HttpGet("unread-count")]
        public IActionResult UnreadCount()
        {
            var uid = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var count = _ctx.UserNotifications.Count(n => n.UserId == uid && !n.IsRead);
            return Ok(new { count });
        }

        // PATCH /api/notifications/{id}/read
        [HttpPatch("{id:long}/read")]
        public IActionResult MarkRead(long id)
        {
            var uid = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var noti = _ctx.UserNotifications.FirstOrDefault(n => n.Id == id && n.UserId == uid);
            if (noti == null) return NotFound();

            noti.IsRead = true;
            _ctx.SaveChanges();
            return NoContent();
        }

        // PATCH /api/notifications/read-all
        [HttpPatch("read-all")]
        public IActionResult MarkAllRead()
        {
            var uid = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var notis = _ctx.UserNotifications.Where(n => n.UserId == uid && !n.IsRead).ToList();
            foreach (var n in notis) n.IsRead = true;
            _ctx.SaveChanges();
            return NoContent();
        }
    }
}
