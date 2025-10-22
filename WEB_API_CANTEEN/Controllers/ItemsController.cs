using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/items/...
    [Authorize(Roles = "ADMIN")]
    public class ItemsController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        private readonly IWebHostEnvironment _env;

        public ItemsController(SmartCanteenDbContext ctx, IWebHostEnvironment env)
        {
            _ctx = ctx;
            _env = env;
        }

        // GET /api/items?categoryId=&q=&page=1&pageSize=20
        [HttpGet]
        public IActionResult List([FromQuery] long? categoryId, [FromQuery] string? q,
                                  [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = page <= 0 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 200);

            IQueryable<Item> query = _ctx.Items.Include(i => i.CategoryNavigation);

            if (categoryId.HasValue)
                query = query.Where(i => i.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim();
                query = query.Where(i => i.Name.Contains(kw));
            }

            var total = query.Count();

            var data = query
                .OrderBy(i => i.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(i => new ItemAdminListVm
                {
                    Id = i.Id,
                    Name = i.Name,
                    Price = i.Price,
                    ImageUrl = i.ImageUrl,
                    IsAvailableToday = i.IsAvailableToday,
                    CategoryId = i.CategoryId,
                    CategoryName = i.CategoryNavigation != null ? i.CategoryNavigation.Name : i.Category
                })
                .ToList();

            return Ok(new { page, pageSize, total, data });
        }

        // GET /api/items/{id}
        [HttpGet("{id:long}")]
        public IActionResult Get(long id)
        {
            var item = _ctx.Items
                .Include(i => i.CategoryNavigation)
                .Where(i => i.Id == id)
                .Select(i => new ItemAdminDetailVm
                {
                    Id = i.Id,
                    Name = i.Name,
                    Price = i.Price,
                    ImageUrl = i.ImageUrl,
                    IsAvailableToday = i.IsAvailableToday,
                    CategoryId = i.CategoryId,
                    CategoryName = i.CategoryNavigation != null ? i.CategoryNavigation.Name : i.Category,
                    CreatedAt = i.CreatedAt
                })
                .FirstOrDefault();

            if (item == null) return NotFound();
            return Ok(item);
        }

        // POST /api/items  (multipart/form-data)
        // form fields: name, price, isAvailableToday, categoryId, image (IFormFile)
        [HttpPost]
        [RequestSizeLimit(20_000_000)] // 20 MB
        public async Task<IActionResult> Create([FromForm] ItemCreateFormDto form)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // 1) Validate categoryId & lấy tên danh mục
            var cat = await _ctx.Categories.FirstOrDefaultAsync(c => c.Id == form.CategoryId && c.IsActive);
            if (cat == null) return BadRequest("Danh mục không tồn tại hoặc đang inactive.");

            // 2) Lưu ảnh (nếu có) vào wwwroot/img và lấy url
            string? imageUrl = null;
            if (form.Image != null && form.Image.Length > 0)
            {
                imageUrl = await SaveImageAsync(form.Image);
            }

            // 3) Tạo item
            var item = new Item
            {
                Name = form.Name,
                Price = form.Price,
                ImageUrl = imageUrl,                  // URL vừa lưu (có thể null)
                IsAvailableToday = form.IsAvailableToday,
                CategoryId = form.CategoryId,
                Category = cat.Name,                  // giữ tương thích schema cũ nếu còn cột string
                CreatedAt = DateTime.UtcNow
            };

            _ctx.Items.Add(item);
            await _ctx.SaveChangesAsync();

            return Ok(new
            {
                item.Id,
                item.Name,
                item.Price,
                item.ImageUrl,
                item.IsAvailableToday,
                item.CategoryId,
                CategoryName = cat.Name
            });
        }

        // PUT /api/items/{id}  (multipart/form-data)
        // form fields: name?, price?, isAvailableToday?, categoryId?, image?(IFormFile)
        [HttpPut("{id:long}")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> Update(long id, [FromForm] ItemUpdateFormDto form)
        {
            var item = await _ctx.Items.FindAsync(id);
            if (item == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(form.Name)) item.Name = form.Name!;
            if (form.Price.HasValue) item.Price = form.Price.Value;
            if (form.IsAvailableToday.HasValue) item.IsAvailableToday = form.IsAvailableToday.Value;

            if (form.CategoryId.HasValue)
            {
                var ok = await _ctx.Categories.AnyAsync(c => c.Id == form.CategoryId.Value && c.IsActive);
                if (!ok) return BadRequest("Danh mục không tồn tại hoặc đang inactive.");
                item.CategoryId = form.CategoryId.Value;

                // đồng bộ tên cột string (nếu còn)
                var catName = await _ctx.Categories.Where(c => c.Id == form.CategoryId.Value).Select(c => c.Name).FirstAsync();
                item.Category = catName;
            }

            // Nếu có upload ảnh mới → lưu + thay url
            if (form.Image != null && form.Image.Length > 0)
            {
                var newUrl = await SaveImageAsync(form.Image);

                // (tuỳ chọn) xoá ảnh cũ trên đĩa nếu cần
                TryDeleteOldImage(item.ImageUrl);

                item.ImageUrl = newUrl;
            }

            await _ctx.SaveChangesAsync();
            return NoContent();
        }

        // PATCH /api/items/{id}/availability
        [HttpPatch("{id:long}/availability")]
        public IActionResult UpdateAvailability(long id, [FromBody] AdminItemAvailabilityDto dto)
        {
            var item = _ctx.Items.FirstOrDefault(i => i.Id == id);
            if (item == null) return NotFound();

            item.IsAvailableToday = dto.IsAvailable;
            _ctx.SaveChanges();
            return NoContent();
        }

        // DELETE /api/items/{id}
        [HttpDelete("{id:long}")]
        public IActionResult Delete(long id)
        {
            var item = _ctx.Items.Include(i => i.OrderItems).FirstOrDefault(i => i.Id == id);
            if (item == null) return NotFound();

            if (item.OrderItems.Any())
                return BadRequest("Món đã phát sinh đơn hàng, không thể xóa cứng. Hãy chuyển IsAvailableToday = false.");

            // (tuỳ chọn) xoá ảnh khỏi ổ đĩa
            TryDeleteOldImage(item.ImageUrl);

            _ctx.Items.Remove(item);
            _ctx.SaveChanges();
            return NoContent();
        }

        // ===== Helpers =====
        private async Task<string> SaveImageAsync(IFormFile file)
        {
            // thư mục vật lý: <contentRoot>/wwwroot/img
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var imgDir = Path.Combine(webRoot, "img");
            if (!Directory.Exists(imgDir)) Directory.CreateDirectory(imgDir);

            // tên file an toàn, tránh trùng
            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(imgDir, fileName);

            using (var fs = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            // URL public (client dùng để hiển thị)
            var publicUrl = $"/img/{fileName}";
            return publicUrl;
        }

        private void TryDeleteOldImage(string? url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return;
                if (!url.StartsWith("/img/", StringComparison.OrdinalIgnoreCase)) return;

                var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var physical = Path.Combine(webRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(physical))
                    System.IO.File.Delete(physical);
            }
            catch { /* ignore */ }
        }
    }

    // ===== DTOs & VMs =====
    public class ItemCreateFormDto
    {
        [Required, MaxLength(120)]
        public string Name { get; set; } = "";

        [Range(0, 99_999_999)]
        public decimal Price { get; set; }

        public bool IsAvailableToday { get; set; } = true;

        [Required]
        public long CategoryId { get; set; }

        public IFormFile? Image { get; set; } // file ảnh (tuỳ chọn)
    }

    public class ItemUpdateFormDto
    {
        [MaxLength(120)] public string? Name { get; set; }
        [Range(0, 99_999_999)] public decimal? Price { get; set; }
        public bool? IsAvailableToday { get; set; }
        public long? CategoryId { get; set; }

        public IFormFile? Image { get; set; } // file ảnh mới (tuỳ chọn)
    }

    public class AdminItemAvailabilityDto
    {
        [Required] public bool IsAvailable { get; set; }
    }

    public class ItemAdminListVm
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsAvailableToday { get; set; }
        public long CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }

    public class ItemAdminDetailVm : ItemAdminListVm
    {
        public DateTime CreatedAt { get; set; }
    }
}
