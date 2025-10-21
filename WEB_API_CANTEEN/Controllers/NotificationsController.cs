using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_API_CANTEEN.Models;
using WEB_API_CANTEEN.Services;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IAuditService _audit;
        public NotificationsController(SmartCanteenDbContext ctx, IAuditService audit)
        {
            _ctx = ctx; _audit = audit;
        }

        private long CurrentUserId() =>
            long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        [HttpGet("me")]
        public IActionResult MyNotifications([FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var uid = CurrentUserId();
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);

            var q = _ctx.UserNotifications.AsNoTracking().Where(x => x.UserId == uid);
            if (unreadOnly) q = q.Where(x => !x.IsRead);

            var total = q.Count();
            var items = q.OrderByDescending(x => x.CreatedAt)
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .Select(x => new { x.Id, x.Title, x.Body, x.Type, x.ReferenceId, x.IsRead, x.CreatedAt })
                         .ToList();

            return Ok(new { page, pageSize, total, items });
        }

        [HttpGet("unread-count")]
        public IActionResult UnreadCount()
        {
            var uid = CurrentUserId();
            var cnt = _ctx.UserNotifications.Count(x => x.UserId == uid && !x.IsRead);
            return Ok(new { unread = cnt });
        }

        [HttpPatch("{id:long}/read")]
        public IActionResult MarkRead(long id)
        {
            var uid = CurrentUserId();
            var noti = _ctx.UserNotifications.FirstOrDefault(x => x.Id == id && x.UserId == uid);
            if (noti == null) return NotFound();
            if (!noti.IsRead)
            {
                noti.IsRead = true;
                _ctx.SaveChanges();
                _audit.Log(uid, "NOTI_READ", "UserNotification", id);
            }
            return NoContent();
        }

        [HttpPost("read-all")]
        public IActionResult MarkAllRead()
        {
            var uid = CurrentUserId();
            var list = _ctx.UserNotifications.Where(x => x.UserId == uid && !x.IsRead).ToList();
            foreach (var n in list) n.IsRead = true;
            _ctx.SaveChanges();
            _audit.Log(uid, "NOTI_READ_ALL", "UserNotification");
            return Ok(new { updated = list.Count });
        }
    }
}
