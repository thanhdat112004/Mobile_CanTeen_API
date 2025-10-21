using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PointsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public PointsController(SmartCanteenDbContext ctx) => _ctx = ctx;

        private long CurrentUserId() =>
            long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // GET /api/points/me?page=1&pageSize=20
        [HttpGet("me")]
        public IActionResult MyPoints([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var uid = CurrentUserId();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            // Tổng điểm = SUM(Delta)
            var total = _ctx.PointsLedgers.Where(p => p.UserId == uid).Sum(p => (int?)p.Delta) ?? 0;

            var q = _ctx.PointsLedgers.AsNoTracking()
                                      .Where(p => p.UserId == uid)
                                      .OrderByDescending(p => p.CreatedAt);

            var count = q.Count();
            var items = q.Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .Select(p => new { p.Id, p.OrderId, p.Delta, p.Reason, p.CreatedAt })
                         .ToList();

            return Ok(new { total, page, pageSize, count, items });
        }
    }
}
