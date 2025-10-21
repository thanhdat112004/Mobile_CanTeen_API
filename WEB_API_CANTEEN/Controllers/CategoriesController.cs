// CategoriesController: CRUD danh mục (List cho tất cả; Create/Update/Delete cho ADMIN)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // /api/categories
    public class CategoriesController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public CategoriesController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // GET /api/categories?includeInactive=false
        // => Danh sách danh mục (mặc định chỉ lấy đang active)
        [HttpGet]
        public IActionResult List([FromQuery] bool includeInactive = false)
        {
            var q = _ctx.Categories.AsQueryable();
            if (!includeInactive) q = q.Where(x => x.IsActive);

            var data = q.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                        .Select(x => new CategoryDto
                        {
                            Id = x.Id,
                            Name = x.Name,
                            SortOrder = x.SortOrder,
                            IsActive = x.IsActive
                        })
                        .ToList();

            return Ok(data);
        }

        // POST /api/categories  (ADMIN)
        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        public IActionResult Create([FromBody] CategoryCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (_ctx.Categories.Any(c => c.Name == dto.Name))
                return Conflict("Tên danh mục đã tồn tại");

            var cat = new Category
            {
                Name = dto.Name,
                SortOrder = dto.SortOrder ?? 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _ctx.Categories.Add(cat);
            _ctx.SaveChanges();

            return Ok(new { cat.Id, cat.Name });
        }

        // PUT /api/categories/{id}  (ADMIN)
        [Authorize(Roles = "ADMIN")]
        [HttpPut("{id:long}")]
        public IActionResult Update(long id, [FromBody] CategoryUpdateDto dto)
        {
            var cat = _ctx.Categories.Find(id);
            if (cat == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                if (_ctx.Categories.Any(c => c.Id != id && c.Name == dto.Name))
                    return Conflict("Tên danh mục đã tồn tại");
                cat.Name = dto.Name!;
            }
            if (dto.SortOrder.HasValue) cat.SortOrder = dto.SortOrder.Value;
            if (dto.IsActive.HasValue) cat.IsActive = dto.IsActive.Value;

            _ctx.SaveChanges();
            return NoContent();
        }

        // DELETE /api/categories/{id}  (ADMIN)
        // Nếu danh mục đang có món thì soft-delete (IsActive=false)
        [Authorize(Roles = "ADMIN")]
        [HttpDelete("{id:long}")]
        public IActionResult Delete(long id)
        {
            var cat = _ctx.Categories.Find(id);
            if (cat == null) return NotFound();

            var hasItems = _ctx.Items.Any(i => i.CategoryId == id);
            if (hasItems)
            {
                cat.IsActive = false;
                _ctx.SaveChanges();
                return Ok(new { message = "Danh mục đang có món. Đã chuyển sang trạng thái inactive." });
            }

            _ctx.Categories.Remove(cat);
            _ctx.SaveChanges();
            return NoContent();
        }
    }

    // ===== DTOs =====
    public class CategoryDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class CategoryCreateDto
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";
        public int? SortOrder { get; set; }
    }

    public class CategoryUpdateDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsActive { get; set; }
    }
}
