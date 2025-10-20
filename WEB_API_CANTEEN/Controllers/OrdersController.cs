// OrdersController: tạo đơn, lịch sử, hủy, xuất hóa đơn PDF (QuestPDF)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_API_CANTEEN.Models;
using WEB_API_CANTEEN.Services; // IInvoiceService

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/orders
    public class OrdersController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IInvoiceService _invoice;
        public OrdersController(SmartCanteenDbContext ctx, IInvoiceService invoice)
        {
            _ctx = ctx;
            _invoice = invoice;
        }

        // GET /api/orders/me
        [Authorize]
        [HttpGet("me")]
        public IActionResult MyOrders()
        {
            var uidStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(uidStr)) return Unauthorized();
            var uid = long.Parse(uidStr);

            var data = _ctx.Orders
                .Where(o => o.UserId == uid)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.Total,
                    o.Status,
                    o.PaymentStatus,
                    o.PaymentMethod,
                    o.CreatedAt,
                    Items = _ctx.OrderItems.Where(oi => oi.OrderId == o.Id)
                            .Select(oi => new { oi.ItemId, Name = oi.Item.Name, oi.Qty, oi.Note })
                })
                .ToList();

            return Ok(data);
        }

        // POST /api/orders
        [Authorize]
        [HttpPost]
        public IActionResult Create([FromBody] CreateOrderDto dto)
        {
            var uidStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(uidStr)) return Unauthorized();
            var uid = long.Parse(uidStr);

            if (dto.Items == null || dto.Items.Count == 0)
                return BadRequest("Danh sách món trống");

            var total = dto.Items.Sum(i => _ctx.Items.Where(x => x.Id == i.ItemId).Select(x => x.Price).First() * i.Qty);

            var order = new Order
            {
                UserId = uid,
                Status = "PENDING",
                PaymentMethod = dto.PaymentMethod, // CASH | INTERNAL_QR
                PaymentStatus = "UNPAID",
                EtaMinutes = dto.EtaMinutes ?? 10,
                Note = dto.Note,
                Total = total,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.Orders.Add(order);
            _ctx.SaveChanges();

            foreach (var it in dto.Items)
                _ctx.OrderItems.Add(new OrderItem { OrderId = order.Id, ItemId = it.ItemId, Qty = it.Qty, Note = it.Note });

            _ctx.SaveChanges();

            return Ok(new { order.Id, order.Total, order.Status, order.PaymentMethod, order.PaymentStatus });
        }

        // PATCH /api/orders/{id}/cancel
        [Authorize]
        [HttpPatch("{id}/cancel")]
        public IActionResult Cancel(long id)
        {
            var order = _ctx.Orders.Find(id);
            if (order == null) return NotFound();
            if (!string.Equals(order.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Chỉ hủy khi đơn còn PENDING");

            if (string.Equals(order.PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase))
            {
                order.PaymentStatus = "REFUNDED";
                _ctx.PaymentTransactions.Add(new PaymentTransaction
                {
                    OrderId = order.Id,
                    Method = order.PaymentMethod ?? "CASH",
                    Action = "REFUND",
                    Status = "SUCCESS",
                    RefCode = $"RF-{order.Id}",
                    Amount = order.Total,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _ctx.SaveChanges();
            return NoContent();
        }

        // GET /api/orders/{id}/invoice
        [Authorize]
        [HttpGet("{id}/invoice")]
        public IActionResult Invoice(long id)
        {
            if (!_ctx.Orders.Any(o => o.Id == id)) return NotFound();
            var pdf = _invoice.GenerateInvoice(id);
            return File(pdf, "application/pdf", $"invoice-{id}.pdf");
        }
    }

    // ===== DTOs =====
    public class CreateOrderDto
    {
        public string PaymentMethod { get; set; } = "CASH"; // CASH | INTERNAL_QR
        public int? EtaMinutes { get; set; }
        public string? Note { get; set; }
        public List<CreateOrderItemDto> Items { get; set; } = new();
    }
    public class CreateOrderItemDto
    {
        public long ItemId { get; set; }
        public int Qty { get; set; }
        public string? Note { get; set; }
    }
}
