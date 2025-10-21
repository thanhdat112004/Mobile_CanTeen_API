using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class Category
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<Item> Items { get; set; } = new List<Item>();
}
