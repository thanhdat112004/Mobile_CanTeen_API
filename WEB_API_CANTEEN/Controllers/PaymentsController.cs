using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using WEB_API_CANTEEN.Models;
using WEB_API_CANTEEN.Services;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IAuditService _audit;
        public PaymentsController(SmartCanteenDbContext ctx, IAuditService audit)
        {
            _ctx = ctx; _audit = audit;
        }

        private long CurrentUserId() =>
            long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // POST /api/payments/intent
        [HttpPost("intent")]
        public IActionResult Intent([FromBody] PaymentIntentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var order = _ctx.Orders.FirstOrDefault(o => o.Id == dto.OrderId);
            if (order == null) return NotFound("Order không tồn tại.");
            if (order.PaymentStatus == "PAID") return BadRequest("Đơn đã thanh toán.");

            if (!string.IsNullOrWhiteSpace(dto.Method))
                order.PaymentMethod = dto.Method.Trim().ToUpperInvariant();

            var refCode = $"INT-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            _ctx.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Method = order.PaymentMethod ?? "CASH",
                Action = "INTENT",
                Status = "PENDING",
                RefCode = refCode,
                Amount = order.Total,
                CreatedAt = DateTime.UtcNow
            });
            _ctx.SaveChanges();

            _audit.Log(CurrentUserId(), "PAYMENT_INTENT", "Order", order.Id);

            var qrPayload = $"ORDER:{order.Id}|AMT:{order.Total}|REF:{refCode}";
            return Ok(new { order.Id, order.Total, order.PaymentMethod, order.PaymentStatus, refCode, qrPayload });
        }

        // POST /api/payments/{orderId}/capture
        [HttpPost("{orderId:long}/capture")]
        public IActionResult Capture(long orderId, [FromBody] PaymentCaptureDto dto)
        {
            var order = _ctx.Orders.Include(o => o.OrderItems).FirstOrDefault(o => o.Id == orderId);
            if (order == null) return NotFound("Order không tồn tại.");
            if (order.PaymentStatus == "PAID") return BadRequest("Đơn đã thanh toán.");

            var uid = CurrentUserId();
            var isPrivileged = User.IsInRole("ADMIN") || User.IsInRole("STAFF");
            if (!isPrivileged && order.UserId != uid) return Forbid();

            order.PaymentStatus = "PAID";
            order.PaidAt = DateTime.UtcNow;

            _ctx.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Method = order.PaymentMethod ?? "CASH",
                Action = "CAPTURE",
                Status = "SUCCESS",
                RefCode = string.IsNullOrWhiteSpace(dto.RefCode) ? $"CAP-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}" : dto.RefCode,
                Amount = order.Total,
                ActorId = uid,
                CreatedAt = DateTime.UtcNow
            });

            if (!string.IsNullOrWhiteSpace(dto.Note))
                order.Note = string.IsNullOrWhiteSpace(order.Note) ? dto.Note : $"{order.Note} | {dto.Note}";

            // Điểm thưởng: 1 điểm/10.000đ (ghi cả Delta & Points để khớp model)
            var earn = (int)Math.Floor(order.Total / 10000m);
            if (earn > 0)
            {
                _ctx.PointsLedgers.Add(new PointsLedger
                {
                    UserId = order.UserId,
                    OrderId = order.Id,
                    Delta = earn,
                    Points = earn,
                    Reason = "PAYMENT_CAPTURE",
                    CreatedAt = DateTime.UtcNow
                });

                _ctx.UserNotifications.Add(new UserNotification
                {
                    UserId = order.UserId,
                    Title = $"+{earn} điểm thưởng",
                    Body = $"Đơn #{order.Id} đã thanh toán. Bạn nhận được {earn} điểm.",
                    Type = "POINTS_ADD",
                    ReferenceId = order.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }

            // Voucher: chuyển PENDING -> SUCCESS + tăng Used
            var vtx = _ctx.PaymentTransactions
                         .Where(t => t.OrderId == orderId && t.Action == "VOUCHER_APPLY" && t.Status == "PENDING")
                         .ToList();
            foreach (var t in vtx)
            {
                t.Status = "SUCCESS";
                if (!string.IsNullOrWhiteSpace(t.RefCode))
                {
                    var v = _ctx.Vouchers.FirstOrDefault(x => x.Code == t.RefCode);
                    if (v != null)
                    {
                        v.Used = v.Used + 1;  // hoặc v.Used += 1;
                        _audit.Log(uid, "VOUCHER_USED", "Voucher", v.Id);
                    }
                }
            }

            _ctx.UserNotifications.Add(new UserNotification
            {
                UserId = order.UserId,
                Title = $"Đơn #{order.Id} đã thanh toán",
                Body = $"Số tiền: {order.Total:N0} đ. Cảm ơn bạn!",
                Type = "PAYMENT_SUCCESS",
                ReferenceId = order.Id,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            });

            _ctx.SaveChanges();
            _audit.Log(uid, "PAYMENT_CAPTURE", "Order", order.Id);

            return Ok(new { order.Id, order.PaymentStatus, order.PaidAt, pointsEarned = earn });
        }
    }

    public class PaymentIntentDto
    {
        [Required] public long OrderId { get; set; }
        public string? Method { get; set; } // CASH | INTERNAL_QR
    }
    public class PaymentCaptureDto
    {
        public string? RefCode { get; set; }
        public string? Note { get; set; }
    }
}
