// KdsController: Màn hình bếp (STAFF/ADMIN) xem & đổi trạng thái đơn
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/kds
    [Authorize(Roles = "STAFF,ADMIN")]
    public class KdsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public KdsController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // GET /api/kds/tickets?status=
        [HttpGet("tickets")]
        public IActionResult Tickets([FromQuery] string? status = null)
        {
            var q = _ctx.Orders.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(o => o.Status == status);
            var data = q.OrderBy(o => o.CreatedAt)
                        .Select(o => new { o.Id, o.Status, o.PaymentStatus, o.Total, o.CreatedAt })
                        .ToList();
            return Ok(data);
        }

        // PATCH /api/kds/tickets/{id}
        [HttpPatch("tickets/{id}")]
        public IActionResult UpdateStatus(long id, [FromBody] KdsUpdateDto dto)
        {
            var order = _ctx.Orders.Find(id);
            if (order == null) return NotFound();
            order.Status = dto.Status; // IN_PROGRESS | READY | PICKED_UP
            _ctx.SaveChanges();
            return NoContent();
        }
    }

    public class KdsUpdateDto { public string Status { get; set; } = "IN_PROGRESS"; }
}
