// MenuController: danh sách món, trạng thái còn/hết (STAFF/ADMIN cập nhật)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/menu
    public class MenuController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public MenuController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // GET /api/menu/items?category=
        [Authorize]
        [HttpGet("items")]
        public IActionResult Items([FromQuery] string? category = null)
        {
            var q = _ctx.Items.AsQueryable();
            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(x => x.Category == category);
            return Ok(q.OrderBy(x => x.Name).ToList());
        }

        // GET /api/menu/availability
        [Authorize]
        [HttpGet("availability")]
        public IActionResult Availability()
        {
            var data = _ctx.Items.Select(x => new { x.Id, x.Name, x.IsAvailableToday }).ToList();
            return Ok(data);
        }

        // PATCH /api/menu/items/{id}/availability
        [Authorize(Roles = "ADMIN,STAFF")]
        [HttpPatch("items/{id}/availability")]
        public IActionResult SetAvailability(long id, [FromBody] AvailabilityDto dto)
        {
            var item = _ctx.Items.Find(id);
            if (item == null) return NotFound();
            item.IsAvailableToday = dto.IsAvailable;
            _ctx.SaveChanges();
            return NoContent();
        }
    }

    public class AvailabilityDto { public bool IsAvailable { get; set; } }
}
