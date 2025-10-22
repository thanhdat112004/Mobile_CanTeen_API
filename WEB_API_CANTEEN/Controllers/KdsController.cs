using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    /// <summary>
    /// KDS cho STAFF/ADMIN:
    /// - Xem hàng đợi đơn
    /// - Cập nhật trạng thái: PENDING -> IN_PROGRESS -> READY -> PICKED_UP
    /// - Tự tạo thông báo khi READY/PICKED_UP
    /// </summary>
    [ApiController]
    [Route("api/kds")]
    [Authorize(Roles = "STAFF,ADMIN")]
    public class KdsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public KdsController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // GET /api/kds/queue?status=PENDING,IN_PROGRESS,READY&page=1&pageSize=20
        [HttpGet("queue")]
        public IActionResult Queue([FromQuery] string? status = null,
                                   [FromQuery] int page = 1,
                                   [FromQuery] int pageSize = 20)
        {
            page = page <= 0 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var statuses = ParseStatuses(status);

            var q = _ctx.Orders
                        .Where(o => statuses.Contains(o.Status))
                        .OrderBy(o => o.Status)
                        .ThenBy(o => o.CreatedAt)
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.Item);

            var total = q.Count();

            var data = q.Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(o => new
                        {
                            o.Id,
                            o.UserId,
                            o.Status,
                            o.PaymentStatus,
                            o.Total,
                            o.CreatedAt,
                            Items = o.OrderItems.Select(oi => new
                            {
                                oi.ItemId,
                                ItemName = oi.Item.Name,
                                oi.Qty,
                                oi.Note
                            })
                        })
                        .ToList();

            return Ok(new { page, pageSize, total, data });
        }

        // PATCH /api/kds/orders/{id}/status
        // body: { "status": "IN_PROGRESS|READY|PICKED_UP", "note": "..." }
        [HttpPatch("orders/{id:long}/status")]
        public IActionResult UpdateStatus(long id, [FromBody] KdsStatusDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var order = _ctx.Orders
                            .Include(o => o.OrderItems)
                            .FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound("Order không tồn tại.");

            var from = (order.Status ?? "").ToUpperInvariant();
            var to = (dto.Status ?? "").Trim().ToUpperInvariant();

            if (!IsValidTransition(from, to))
                return BadRequest($"Chuyển trạng thái không hợp lệ: {from} -> {to}");

            order.Status = to;

            if (!string.IsNullOrWhiteSpace(dto.Note))
            {
                order.Note = string.IsNullOrWhiteSpace(order.Note)
                    ? dto.Note
                    : $"{order.Note} | {dto.Note}";
            }

            // Lấy actor id (nếu muốn log)
            long? actorId = null;
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(claimId, out var parsed)) actorId = parsed;

            // ✅ SỬA Ở ĐÂY: dùng CreatedAt thay vì At
            _ctx.AuditLogs.Add(new AuditLog
            {
                ActorId = actorId,
                Action = $"KDS_STATUS_{to}",
                Entity = "Order",
                EntityId = order.Id,
                CreatedAt = DateTime.UtcNow
            });

            // Tạo thông báo cho user khi READY/PICKED_UP
            if (to == "READY")
            {
                AddNotification(order.UserId,
                    $"Đơn #{order.Id} đã sẵn sàng",
                    "Vui lòng tới quầy nhận món.",
                    "ORDER_STATUS",
                    order.Id);
            }
            else if (to == "PICKED_UP")
            {
                AddNotification(order.UserId,
                    $"Đơn #{order.Id} đã được nhận",
                    "Chúc bạn ngon miệng!",
                    "ORDER_STATUS",
                    order.Id);
            }

            _ctx.SaveChanges();

            return Ok(new
            {
                order.Id,
                order.Status,
                order.PaymentStatus,
                order.Total
            });
        }

        // ===== Helpers =====

        private static bool IsValidTransition(string from, string to)
        {
            if (from == to) return false;

            return (from, to) switch
            {
                ("PENDING", "IN_PROGRESS") => true,
                ("IN_PROGRESS", "READY") => true,
                ("READY", "PICKED_UP") => true,
                _ => false
            };
        }

        private static HashSet<string> ParseStatuses(string? statusCsv)
        {
            var defaults = new[] { "PENDING", "IN_PROGRESS", "READY" };
            if (string.IsNullOrWhiteSpace(statusCsv))
                return new HashSet<string>(defaults);

            return statusCsv
                   .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Select(s => s.ToUpperInvariant())
                   .ToHashSet();
        }

        private void AddNotification(long userId, string title, string body, string type, long? refId)
        {
            _ctx.UserNotifications.Add(new UserNotification
            {
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                ReferenceId = refId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    public class KdsStatusDto
    {
        [Required]
        public string Status { get; set; } = "";
        public string? Note { get; set; }
    }
}
