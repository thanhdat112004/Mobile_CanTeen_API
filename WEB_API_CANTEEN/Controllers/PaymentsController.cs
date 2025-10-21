using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // mọi action cần đăng nhập
    public class PaymentsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public PaymentsController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // POST /api/payments/intent
        // body: { "orderId": 123, "method": "CASH|INTERNAL_QR" }
        [HttpPost("intent")]
        public IActionResult Intent([FromBody] PaymentIntentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var order = _ctx.Orders.FirstOrDefault(o => o.Id == dto.OrderId);
            if (order == null) return NotFound("Order không tồn tại.");
            if (order.PaymentStatus == "PAID") return BadRequest("Đơn đã thanh toán.");

            // Lưu phương thức nếu có gửi lên
            if (!string.IsNullOrWhiteSpace(dto.Method))
                order.PaymentMethod = dto.Method.Trim().ToUpperInvariant();

            // Ghi giao dịch INTENT
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

            // Payload QR (nếu cần client tạo QR)
            var qrPayload = $"ORDER:{order.Id}|AMT:{order.Total}|REF:{refCode}";

            return Ok(new
            {
                order.Id,
                order.Total,
                order.PaymentMethod,
                order.PaymentStatus,
                refCode,
                qrPayload
            });
        }

        // POST /api/payments/{orderId}/capture
        // body: { "refCode": "..." , "note": "..." }
        [HttpPost("{orderId:long}/capture")]
        public IActionResult Capture(long orderId, [FromBody] PaymentCaptureDto dto)
        {
            var order = _ctx.Orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null) return NotFound("Order không tồn tại.");
            if (order.PaymentStatus == "PAID") return BadRequest("Đơn đã thanh toán.");

            // Bảo vệ: user chỉ capture đơn của mình, trừ ADMIN/STAFF
            var uid = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isPrivileged = User.IsInRole("ADMIN") || User.IsInRole("STAFF");
            if (!isPrivileged && order.UserId != uid) return Forbid();

            // Cập nhật thanh toán
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

            // (tùy chọn) Lưu note vào order
            if (!string.IsNullOrWhiteSpace(dto.Note))
            {
                order.Note = string.IsNullOrWhiteSpace(order.Note)
                    ? dto.Note
                    : $"{order.Note} | {dto.Note}";
            }

            _ctx.SaveChanges();

            // === CỘNG ĐIỂM === (1 điểm / 10.000đ)
            var earn = (int)Math.Floor(order.Total / 10000m);
            if (earn > 0)
            {
                _ctx.PointsLedgers.Add(new PointsLedger
                {
                    UserId = order.UserId,
                    OrderId = order.Id,
                    Points = earn,
                    Reason = $"Hoàn tất đơn #{order.Id}",
                    CreatedAt = DateTime.UtcNow
                });
                _ctx.SaveChanges();
            }

            // === THÔNG BÁO ===
            _ctx.UserNotifications.Add(new UserNotification
            {
                UserId = order.UserId,
                Title = $"Thanh toán thành công cho đơn #{order.Id}",
                Body = $"Số tiền: {order.Total:N0} đ. Cảm ơn bạn!",
                Type = "PAYMENT",
                ReferenceId = order.Id,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            _ctx.SaveChanges();

            return Ok(new
            {
                order.Id,
                order.PaymentStatus,
                order.PaidAt,
                pointsEarned = earn
            });
        }
    }

    public class PaymentIntentDto
    {
        [Required]
        public long OrderId { get; set; }

        public string? Method { get; set; } // CASH | INTERNAL_QR
    }

    public class PaymentCaptureDto
    {
        public string? RefCode { get; set; }
        public string? Note { get; set; }
    }
}
