using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class Voucher
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Type { get; set; } = null!;

    public decimal Value { get; set; }

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public int? Quota { get; set; }

    public int Used { get; set; }

    public DateTime CreatedAt { get; set; }
}
