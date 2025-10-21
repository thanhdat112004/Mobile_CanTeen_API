using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class Item
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Category { get; set; }

    public decimal Price { get; set; }

    public bool IsAvailableToday { get; set; }

    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public long CategoryId { get; set; }

    public virtual Category CategoryNavigation { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
