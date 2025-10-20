// PaymentsController: QR nội bộ (intent/capture/refund)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/payments
    public class PaymentsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IConfiguration _cfg;
        public PaymentsController(SmartCanteenDbContext ctx, IConfiguration cfg) { _ctx = ctx; _cfg = cfg; }

        // POST /api/payments/intents
        [Authorize]
        [HttpPost("intents")]
        public IActionResult CreateIntent([FromBody] PaymentIntentDto dto)
        {
            var order = _ctx.Orders.Find(dto.OrderId);
            if (order == null) return NotFound();
            if (order.PaymentStatus == "PAID") return BadRequest("Đơn đã thanh toán");

            var secret = _cfg["Payments:QrSecret"] ?? "qr-hmac-secret";
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = $"{order.Id}|{order.Total}|{ts}";
            var sig = Convert.ToHexString(new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(payload)));
            var refCode = $"INT-{order.Id}-{ts}";
            var qrPayload = $"CAN_TIN|ORDER:{order.Id}|AMT:{order.Total}|TS:{ts}|SIG:{sig}";

            _ctx.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Method = order.PaymentMethod ?? "INTERNAL_QR",
                Action = "INTENT",
                Status = "SUCCESS",
                RefCode = refCode,
                Amount = order.Total,
                CreatedAt = DateTime.UtcNow
            });
            _ctx.SaveChanges();

            return Ok(new { qrPayload, refCode, expiresAt = DateTime.UtcNow.AddMinutes(5) });
        }

        // POST /api/payments/capture
        [Authorize(Roles = "STAFF,ADMIN")]
        [HttpPost("capture")]
        public IActionResult Capture([FromBody] PaymentCaptureDto dto)
        {
            var order = _ctx.Orders.Find(dto.OrderId);
            if (order == null) return NotFound();
            if (order.PaymentStatus == "PAID") return Conflict("Đã capture trước đó");

            order.PaymentStatus = "PAID";
            order.PaidAt = DateTime.UtcNow;
            order.PaymentRef = dto.RefCode;

            _ctx.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Method = order.PaymentMethod ?? "INTERNAL_QR",
                Action = "CAPTURE",
                Status = "SUCCESS",
                RefCode = dto.RefCode,
                Amount = order.Total,
                CreatedAt = DateTime.UtcNow
            });
            _ctx.SaveChanges();
            return NoContent();
        }

        // POST /api/payments/refund
        [Authorize(Roles = "ADMIN,STAFF")]
        [HttpPost("refund")]
        public IActionResult Refund([FromBody] RefundDto dto)
        {
            var order = _ctx.Orders.Find(dto.OrderId);
            if (order == null) return NotFound();

            order.PaymentStatus = "REFUNDED";
            _ctx.PaymentTransactions.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                Method = order.PaymentMethod ?? "INTERNAL_QR",
                Action = "REFUND",
                Status = "SUCCESS",
                RefCode = $"RF-{order.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                Amount = order.Total,
                CreatedAt = DateTime.UtcNow
            });
            _ctx.SaveChanges();
            return NoContent();
        }
    }

    // ===== DTOs =====
    public class PaymentIntentDto { public long OrderId { get; set; } }
    public class PaymentCaptureDto { public long OrderId { get; set; } public string RefCode { get; set; } = ""; }
    public class RefundDto { public long OrderId { get; set; } public string? Reason { get; set; } }
}
