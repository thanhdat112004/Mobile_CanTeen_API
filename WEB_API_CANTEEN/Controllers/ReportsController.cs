using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "ADMIN")]
    public class ReportsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public ReportsController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // ==== OVERVIEW: hôm nay / tuần này / tháng này ====
        // GET /api/reports/overview?tzOffsetMinutes=420
        [HttpGet("overview")]
        public IActionResult Overview([FromQuery] int tzOffsetMinutes = 0)
        {
            var nowUtc = DateTime.UtcNow;

            // local boundaries
            DateTime TodayStartLocal(DateTime dt) => dt.AddMinutes(tzOffsetMinutes).Date;
            var todayStartLocal = TodayStartLocal(nowUtc);
            var weekStartLocal = StartOfIsoWeek(todayStartLocal);
            var monthStartLocal = new DateTime(todayStartLocal.Year, todayStartLocal.Month, 1);

            // convert back to UTC ranges for DB filter
            var todayStartUtc = todayStartLocal.AddMinutes(-tzOffsetMinutes);
            var weekStartUtc = weekStartLocal.AddMinutes(-tzOffsetMinutes);
            var monthStartUtc = monthStartLocal.AddMinutes(-tzOffsetMinutes);
            var tomorrowStartUtc = todayStartUtc.AddDays(1);

            var paid = _ctx.Orders.Where(o => o.PaymentStatus == "PAID");

            var today = paid.Where(o => o.CreatedAt >= todayStartUtc && o.CreatedAt < tomorrowStartUtc)
                            .GroupBy(_ => 1)
                            .Select(g => new { orders = g.Count(), revenue = g.Sum(x => x.Total) })
                            .FirstOrDefault() ?? new { orders = 0, revenue = 0m };

            var thisWeek = paid.Where(o => o.CreatedAt >= weekStartUtc && o.CreatedAt < tomorrowStartUtc)
                               .GroupBy(_ => 1)
                               .Select(g => new { orders = g.Count(), revenue = g.Sum(x => x.Total) })
                               .FirstOrDefault() ?? new { orders = 0, revenue = 0m };

            var thisMonth = paid.Where(o => o.CreatedAt >= monthStartUtc && o.CreatedAt < tomorrowStartUtc)
                                .GroupBy(_ => 1)
                                .Select(g => new { orders = g.Count(), revenue = g.Sum(x => x.Total) })
                                .FirstOrDefault() ?? new { orders = 0, revenue = 0m };

            // Average Order Value (AOV)
            decimal Aov(int orders, decimal revenue) => orders == 0 ? 0 : Math.Round(revenue / orders, 0);

            return Ok(new
            {
                tzOffsetMinutes,
                today = new { today.orders, today.revenue, aov = Aov(today.orders, today.revenue) },
                week = new { thisWeek.orders, thisWeek.revenue, aov = Aov(thisWeek.orders, thisWeek.revenue) },
                month = new { thisMonth.orders, thisMonth.revenue, aov = Aov(thisMonth.orders, thisMonth.revenue) }
            });
        }

        // ==== REVENUE theo day/week/month với khoảng ngày tự chọn ====
        // GET /api/reports/revenue?granularity=day|week|month&from=2025-10-01&to=2025-10-21&tzOffsetMinutes=420
        [HttpGet("revenue")]
        public IActionResult Revenue(
            [FromQuery] string granularity = "day",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int tzOffsetMinutes = 0)
        {
            var utcNow = DateTime.UtcNow;
            var fromUtc = from?.Date ?? utcNow.Date.AddDays(-7);
            var toUtc = (to?.Date ?? utcNow.Date).AddDays(1); // exclusive
            if (fromUtc >= toUtc) return BadRequest("Khoảng thời gian không hợp lệ.");

            var paid = _ctx.Orders
                .Where(o => o.PaymentStatus == "PAID" && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
                .Select(o => new { o.Id, o.Total, o.CreatedAt })
                .AsNoTracking()
                .ToList();

            var local = paid.Select(o => new { o.Id, o.Total, LocalTime = o.CreatedAt.AddMinutes(tzOffsetMinutes) });

            granularity = (granularity ?? "day").Trim().ToLowerInvariant();
            IEnumerable<GroupBucket> buckets;

            switch (granularity)
            {
                case "month":
                    buckets = local.GroupBy(x => new { x.LocalTime.Year, x.LocalTime.Month })
                        .Select(g => new GroupBucket
                        {
                            Key = $"{g.Key.Year:D4}-{g.Key.Month:D2}",
                            Start = new DateTime(g.Key.Year, g.Key.Month, 1),
                            Orders = g.Count(),
                            Revenue = g.Sum(i => i.Total)
                        })
                        .OrderBy(b => b.Start).ToList();
                    break;

                case "week":
                    buckets = local.GroupBy(x => StartOfIsoWeek(x.LocalTime))
                        .Select(g => new GroupBucket
                        {
                            Key = $"{g.Key:yyyy-MM-dd} (W{ISOWeek.GetWeekOfYear(g.Key)})",
                            Start = g.Key,
                            Orders = g.Count(),
                            Revenue = g.Sum(i => i.Total)
                        })
                        .OrderBy(b => b.Start).ToList();
                    break;

                case "day":
                default:
                    buckets = local.GroupBy(x => x.LocalTime.Date)
                        .Select(g => new GroupBucket
                        {
                            Key = $"{g.Key:yyyy-MM-dd}",
                            Start = g.Key,
                            Orders = g.Count(),
                            Revenue = g.Sum(i => i.Total)
                        })
                        .OrderBy(b => b.Start).ToList();
                    break;
            }

            return Ok(new
            {
                granularity,
                from = fromUtc,
                to = toUtc,
                tzOffsetMinutes,
                totalOrders = buckets.Sum(b => b.Orders),
                totalRevenue = buckets.Sum(b => b.Revenue),
                data = buckets
            });
        }

        // ==== TOP ITEMS trong khoảng đã thanh toán ====
        // GET /api/reports/top-items?limit=5&from=&to=
        [HttpGet("top-items")]
        public IActionResult TopItems([FromQuery] int limit = 5,
                                      [FromQuery] DateTime? from = null,
                                      [FromQuery] DateTime? to = null)
        {
            limit = Math.Clamp(limit, 1, 50);

            var utcNow = DateTime.UtcNow;
            var fromUtc = from?.Date ?? utcNow.Date.AddDays(-7);
            var toUtc = (to?.Date ?? utcNow.Date).AddDays(1);

            var paidOrderIds = _ctx.Orders
                .Where(o => o.PaymentStatus == "PAID" && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc)
                .Select(o => o.Id);

            var q = from oi in _ctx.OrderItems
                    where paidOrderIds.Contains(oi.OrderId)
                    select new { oi.ItemId, oi.Qty, Name = oi.Item.Name, Price = oi.Item.Price };

            var data = q.GroupBy(x => new { x.ItemId, x.Name })
                        .Select(g => new
                        {
                            itemId = g.Key.ItemId,
                            name = g.Key.Name,
                            qty = g.Sum(x => x.Qty),
                            revenue = g.Sum(x => x.Qty * x.Price)
                        })
                        .OrderByDescending(x => x.qty).ThenByDescending(x => x.revenue)
                        .Take(limit)
                        .ToList();

            return Ok(data);
        }

        private static DateTime StartOfIsoWeek(DateTime dt)
        {
            var dow = (int)dt.DayOfWeek; // Sunday=0 ... Saturday=6
            var offset = dow == 0 ? 6 : dow - 1;   // Monday=0 ... Sunday=6
            return dt.Date.AddDays(-offset);
        }
    }

    public class GroupBucket
    {
        public string Key { get; set; } = "";
        public DateTime Start { get; set; }
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
    }
}
