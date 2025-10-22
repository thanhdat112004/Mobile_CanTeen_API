using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VouchersController : ControllerBase
    {
        private readonly SmartCanteenDbContext _ctx;
        public VouchersController(SmartCanteenDbContext ctx) => _ctx = ctx;

        // ========= USER: APPLY =========
        [Authorize]
        [HttpPost("apply")]
        public IActionResult Apply([FromBody] ApplyVoucherDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || dto.Subtotal <= 0)
                return BadRequest("Dữ liệu không hợp lệ.");

            var now = DateTime.UtcNow;
            var v = _ctx.Vouchers.FirstOrDefault(x => x.Code == dto.Code);
            if (v == null) return Ok(new { valid = false, message = "Mã không tồn tại." });

            if (v.StartAt.HasValue && now < v.StartAt.Value)
                return Ok(new { valid = false, message = "Chưa tới thời gian áp dụng." });

            if (v.EndAt.HasValue && now > v.EndAt.Value)
                return Ok(new { valid = false, message = "Mã đã hết hạn." });

            int used = v.Used;        // int (non-null)
            int? quota = v.Quota;     // ép về int? cho an toàn
            if (quota.HasValue && used >= quota.Value)
                return Ok(new { valid = false, message = "Mã đã hết lượt sử dụng." });

            decimal discount = 0;
            var type = (v.Type ?? "AMOUNT").ToUpperInvariant();

            if (type == "PERCENT")
                discount = Math.Round(dto.Subtotal * v.Value / 100m, 0, MidpointRounding.AwayFromZero);
            else
                discount = Math.Min(v.Value, dto.Subtotal);

            return Ok(new
            {
                valid = discount > 0,
                type = v.Type,
                value = v.Value,
                discount,
                message = discount > 0 ? "Áp dụng thành công." : "Mã không đủ điều kiện."
            });
        }

        // ========= ADMIN: CRUD =========
        [Authorize(Roles = "ADMIN")]
        [HttpGet]
        public IActionResult List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? q = null)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var vs = _ctx.Vouchers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var kw = q.Trim();
                vs = vs.Where(v => v.Code.Contains(kw));
            }

            var total = vs.Count();
            var data = vs.OrderByDescending(v => v.CreatedAt)
                         .Skip((page - 1) * pageSize)
                         .Take(pageSize)
                         .Select(v => new {
                             v.Id,
                             v.Code,
                             v.Type,
                             v.Value,
                             v.Quota,
                             v.Used,
                             v.StartAt,
                             v.EndAt,
                             v.CreatedAt
                         }).ToList();

            return Ok(new { page, pageSize, total, data });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        public IActionResult Create([FromBody] VoucherCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var code = dto.Code.Trim().ToUpperInvariant();
            if (_ctx.Vouchers.Any(v => v.Code == code)) return BadRequest("Mã đã tồn tại.");

            var type = (dto.Type ?? "AMOUNT").Trim().ToUpperInvariant();
            if (type is not ("AMOUNT" or "PERCENT")) return BadRequest("Type phải là AMOUNT hoặc PERCENT.");

            var v = new Voucher
            {
                Code = code,
                Type = type,
                Value = dto.Value,
                Used = 0,
                StartAt = dto.StartAt,
                EndAt = dto.EndAt,
                CreatedAt = DateTime.UtcNow
            };

            // ✅ Chỉ gán khi có giá trị — tránh int? → int gây CS0266
            if (dto.Quota.HasValue)
                v.Quota = dto.Quota.Value;

            _ctx.Vouchers.Add(v);
            _ctx.SaveChanges();
            return Ok(new { v.Id });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPut("{id:long}")]
        public IActionResult Update(long id, [FromBody] VoucherUpdateDto dto)
        {
            var v = _ctx.Vouchers.Find(id);
            if (v == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Code))
            {
                var code = dto.Code.Trim().ToUpperInvariant();
                if (_ctx.Vouchers.Any(x => x.Code == code && x.Id != id)) return BadRequest("Mã đã tồn tại.");
                v.Code = code;
            }

            if (!string.IsNullOrWhiteSpace(dto.Type))
            {
                var type = dto.Type.Trim().ToUpperInvariant();
                if (type is not ("AMOUNT" or "PERCENT")) return BadRequest("Type phải là AMOUNT hoặc PERCENT.");
                v.Type = type;
            }

            if (dto.Value.HasValue) v.Value = dto.Value.Value;
            if (dto.Quota.HasValue) v.Quota = dto.Quota.Value;
            if (dto.StartAt.HasValue) v.StartAt = dto.StartAt.Value;
            if (dto.EndAt.HasValue) v.EndAt = dto.EndAt.Value;

            _ctx.SaveChanges();
            return NoContent();
        }

        [Authorize(Roles = "ADMIN")]
        [HttpDelete("{id:long}")]
        public IActionResult Delete(long id)
        {
            var v = _ctx.Vouchers.Find(id);
            if (v == null) return NotFound();
            _ctx.Vouchers.Remove(v);
            _ctx.SaveChanges();
            return NoContent();
        }
    }

    public class ApplyVoucherDto
    {
        public string Code { get; set; } = "";
        public decimal Subtotal { get; set; }
    }

    public class VoucherCreateDto
    {
        public string Code { get; set; } = "";
        public string? Type { get; set; } = "AMOUNT"; // AMOUNT | PERCENT
        public decimal Value { get; set; }
        public int? Quota { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
    }

    public class VoucherUpdateDto
    {
        public string? Code { get; set; }
        public string? Type { get; set; } // AMOUNT | PERCENT
        public decimal? Value { get; set; }
        public int? Quota { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
    }
}
