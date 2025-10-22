using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class OrderItem
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long ItemId { get; set; }

    public int Qty { get; set; }

    public string? Note { get; set; }

    public virtual Item Item { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
