// MenuController: dùng schema cũ (Item.Category = string)
// => Lọc theo tên category, không dùng CategoryId / navigation
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/menu/...
    public class MenuController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public MenuController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // GET /api/menu/categories
        // Lấy danh sách category duy nhất từ Items.Category (string)
        [AllowAnonymous]
        [HttpGet("categories")]
        public IActionResult Categories()
        {
            var cats = _ctx.Items
                           .Where(i => !string.IsNullOrEmpty(i.Category))
                           .Select(i => i.Category!)
                           .Distinct()
                           .OrderBy(x => x)
                           .Select((name, idx) => new { // tạo id tạm nếu cần
                               Id = idx + 1,
                               Name = name
                           })
                           .ToList();

            return Ok(cats);
        }

        // GET /api/menu/items?category=&q=&availableOnly=true&sort=name|price&order=asc|desc&page=1&pageSize=20
        // Danh sách món có lọc + phân trang (lọc theo tên category dạng string)
        [Authorize]
        [HttpGet("items")]
        public IActionResult Items(
            [FromQuery] string? category = null,
            [FromQuery] string? q = null,
            [FromQuery] bool availableOnly = false,
            [FromQuery] string sort = "name",
            [FromQuery] string order = "asc",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            page = page <= 0 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 200);

            IQueryable<Item> query = _ctx.Items;

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(i => i.Category == category);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(i => i.Name.Contains(keyword));
            }

            if (availableOnly)
                query = query.Where(i => i.IsAvailableToday);

            bool asc = !string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
            query = (sort?.ToLowerInvariant()) switch
            {
                "price" => asc ? query.OrderBy(i => i.Price) : query.OrderByDescending(i => i.Price),
                "name" => asc ? query.OrderBy(i => i.Name) : query.OrderByDescending(i => i.Name),
                _ => asc ? query.OrderBy(i => i.Name) : query.OrderByDescending(i => i.Name),
            };

            var total = query.Count();

            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new ItemListDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Price = i.Price,
                    ImageUrl = i.ImageUrl,
                    IsAvailableToday = i.IsAvailableToday,
                    CategoryName = i.Category // <- dùng string Category
                })
                .ToList();

            return Ok(new
            {
                page,
                pageSize,
                total,
                data = items
            });
        }

        // GET /api/menu/items/{id}
        [Authorize]
        [HttpGet("items/{id:long}")]
        public IActionResult ItemDetail(long id)
        {
            var item = _ctx.Items
                           .Where(i => i.Id == id)
                           .Select(i => new ItemDetailDto
                           {
                               Id = i.Id,
                               Name = i.Name,
                               Price = i.Price,
                               ImageUrl = i.ImageUrl,
                               IsAvailableToday = i.IsAvailableToday,
                               CategoryName = i.Category, // <- string
                               CreatedAt = i.CreatedAt
                           })
                           .FirstOrDefault();

            if (item == null) return NotFound();
            return Ok(item);
        }

        // GET /api/menu/availability
        [Authorize]
        [HttpGet("availability")]
        public IActionResult Availability()
        {
            var data = _ctx.Items
                           .OrderBy(i => i.Name)
                           .Select(i => new
                           {
                               i.Id,
                               i.Name,
                               i.IsAvailableToday
                           })
                           .ToList();
            return Ok(data);
        }

        // PATCH /api/menu/items/{id}/availability  (STAFF/ADMIN)
        [Authorize(Roles = "STAFF,ADMIN")]
        [HttpPatch("items/{id:long}/availability")]
        public IActionResult UpdateAvailability(long id, [FromBody] UpdateAvailabilityDto dto)
        {
            var item = _ctx.Items.FirstOrDefault(i => i.Id == id);
            if (item == null) return NotFound();

            item.IsAvailableToday = dto.IsAvailable;
            _ctx.SaveChanges();
            return NoContent();
        }
    }

    // ===== DTOs (giữ CategoryName là string) =====
    public class ItemListDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsAvailableToday { get; set; }
        public string? CategoryName { get; set; } // string
    }

    public class ItemDetailDto : ItemListDto
    {
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateAvailabilityDto
    {
        [Required]
        public bool IsAvailable { get; set; }
    }
}
