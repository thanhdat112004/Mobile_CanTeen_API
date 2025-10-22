using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class PointsLedger
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long? OrderId { get; set; }

    public int Delta { get; set; }

    public int? Points { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Order? Order { get; set; }

    public virtual User User { get; set; } = null!;
}
