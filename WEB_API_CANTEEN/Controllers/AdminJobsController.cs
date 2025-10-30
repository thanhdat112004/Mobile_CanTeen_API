using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/admin/jobs")]
    [Authorize(Roles = "ADMIN")]
    public class AdminJobsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public AdminJobsController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // POST /api/admin/jobs/cancel-stale?minutes=15
        [HttpPost("cancel-stale")]
        public IActionResult CancelStale([FromQuery] int minutes = 15)
        {
            var thresholdUtc = DateTime.UtcNow.AddMinutes(-minutes);

            var stale = _ctx.Orders
                .Where(o => o.Status == "PENDING"
                         && o.PaymentStatus == "UNPAID"
                         && o.CreatedAt <= thresholdUtc)
                .ToList();

            foreach (var o in stale)
            {
                o.Status = "CANCELLED";

                _ctx.UserNotifications.Add(new UserNotification
                {
                    UserId = o.UserId,
                    Title = $"Đơn #{o.Id} đã bị hủy tự động",
                    Body = $"Đơn PENDING quá {minutes} phút không thanh toán.",
                    Type = "ORDER_STATUS",
                    ReferenceId = o.Id,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow                                         
                });

                _ctx.AuditLogs.Add(new AuditLog
                {
                    ActorId = null,
                    Action = "AUTO_CANCEL(manual)",
                    Entity = "Order",
                    EntityId = o.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _ctx.SaveChanges();
            return Ok(new { cancelled = stale.Count });
        }
    }
}
