using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_API_CANTEEN.Models;
using WEB_API_CANTEEN.Services; // IInvoiceService, IAuditService

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/orders
    public class OrdersController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IInvoiceService _invoice;
        private readonly IAuditService _audit;

        public OrdersController(SmartCanteenDbContext ctx, IInvoiceService invoice, IAuditService audit)
        {
            _ctx = ctx;
            _invoice = invoice;
            _audit = audit;
        }

        private long CurrentUserId() =>
            long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // GET /api/orders/me
        [Authorize]
        [HttpGet("me")]
        public IActionResult MyOrders()
        {
            var uid = CurrentUserId();
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
                    Items = o.OrderItems.Select(oi => new { oi.ItemId, Name = oi.Item.Name, oi.Qty, oi.Note })
                }).ToList();

            return Ok(data);
        }

        // POST /api/orders  (áp dụng voucher nếu có)
        [Authorize]
        [HttpPost]
        public IActionResult Create([FromBody] CreateOrderDto dto)
        {
            var uid = CurrentUserId();
            if (dto.Items == null || dto.Items.Count == 0) return BadRequest("Danh sách món trống");

            // Tính subtotal từ giá hiện tại
            var map = dto.Items.ToDictionary(k => k.ItemId, v => v.Qty);
            var items = _ctx.Items.Where(i => map.Keys.Contains(i.Id))
                                  .Select(i => new { i.Id, i.Price })
                                  .ToList();
            if (items.Count != map.Count) return BadRequest("Có món không tồn tại.");
            var subtotal = items.Sum(x => x.Price * map[x.Id]);

            // Voucher
            decimal discount = 0m; Voucher? voucher = null;
            if (!string.IsNullOrWhiteSpace(dto.VoucherCode))
            {
                var code = dto.VoucherCode.Trim().ToUpperInvariant();
                var now = DateTime.UtcNow;
                voucher = _ctx.Vouchers.FirstOrDefault(v =>
                    v.Code == code &&
                    (v.StartAt == null || v.StartAt <= now) &&
                    (v.EndAt == null || v.EndAt >= now));

                if (voucher == null) return BadRequest("Mã voucher không hợp lệ hoặc hết hạn.");
                // ĐÚNG (Quota, Used là int không nullable)
                if (voucher.Quota > 0 && voucher.Used >= voucher.Quota)
                    return BadRequest("Mã voucher đã hết số lượt.");


                if (voucher.Type == "PERCENT")
                {
                    var percent = Math.Clamp((int)Math.Round(voucher.Value), 0, 100);
                    discount = Math.Round(subtotal * percent / 100m, 0);
                }
                else // FIXED
                {
                    discount = Math.Round(voucher.Value, 0);
                }
                if (discount < 0) discount = 0;
                if (discount > subtotal) discount = subtotal;
            }

            var total = subtotal - discount;

            var order = new Order
            {
                UserId = uid,
                Status = "PENDING",
                PaymentMethod = dto.PaymentMethod ?? "CASH",
                PaymentStatus = "UNPAID",
                EtaMinutes = dto.EtaMinutes ?? 10,
                Note = dto.Note,
                Total = total,
                CreatedAt = DateTime.UtcNow
            };

            _ctx.Orders.Add(order);
            _ctx.SaveChanges(); // => có order.Id

            foreach (var it in dto.Items)
                _ctx.OrderItems.Add(new OrderItem { OrderId = order.Id, ItemId = it.ItemId, Qty = it.Qty, Note = it.Note });

            // Ghi giao dịch voucher (pending) để capture xử lý used
            if (voucher != null && discount > 0)
            {
                _ctx.PaymentTransactions.Add(new PaymentTransaction
                {
                    OrderId = order.Id,
                    Method = "VOUCHER",
                    Action = "VOUCHER_APPLY",
                    Status = "PENDING",
                    RefCode = voucher.Code,
                    Amount = discount,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _ctx.SaveChanges();

            _audit.Log(uid, "ORDER_CREATE", "Order", order.Id);

            return Ok(new
            {
                order.Id,
                order.Total,
                order.Status,
                order.PaymentMethod,
                order.PaymentStatus,
                Subtotal = subtotal,
                Discount = discount,
                Voucher = voucher?.Code
            });
        }

        // PATCH /api/orders/{id}/cancel
        [Authorize]
        [HttpPatch("{id:long}/cancel")]
        public IActionResult Cancel(long id)
        {
            var uid = CurrentUserId();
            var order = _ctx.Orders.FirstOrDefault(o => o.Id == id && o.UserId == uid);
            if (order == null) return NotFound();
            if (!string.Equals(order.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Chỉ hủy khi đơn còn PENDING");

            // Hủy: xoá giao dịch voucher PENDING (chưa capture)
            var vtx = _ctx.PaymentTransactions
                .Where(t => t.OrderId == id && t.Action == "VOUCHER_APPLY" && t.Status == "PENDING")
                .ToList();
            if (vtx.Count > 0) _ctx.PaymentTransactions.RemoveRange(vtx);

            order.Status = "CANCELLED";
            _ctx.SaveChanges();

            _audit.Log(uid, "ORDER_CANCEL", "Order", id);
            return NoContent();
        }

        // GET /api/orders/{id}/invoice
        [Authorize]
        [HttpGet("{id:long}/invoice")]
        public IActionResult Invoice(long id)
        {
            if (!_ctx.Orders.Any(o => o.Id == id)) return NotFound();
            var pdf = _invoice.GenerateInvoice(id);
            _audit.Log(CurrentUserId(), "INVOICE_EXPORT", "Order", id);
            return File(pdf, "application/pdf", $"invoice-{id}.pdf");
        }
    }

    public class CreateOrderDto
    {
        public string PaymentMethod { get; set; } = "CASH";
        public int? EtaMinutes { get; set; }
        public string? Note { get; set; }
        public string? VoucherCode { get; set; }   // <== thêm
        public List<CreateOrderItemDto> Items { get; set; } = new();
    }
    public class CreateOrderItemDto
    {
        public long ItemId { get; set; }
        public int Qty { get; set; }
        public string? Note { get; set; }
    }
}
