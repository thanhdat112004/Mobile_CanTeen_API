using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

public partial class Item
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    [StringLength(120)]
    public string Name { get; set; } = null!;

    [Column("category")]
    [StringLength(50)]
    public string? Category { get; set; }

    [Column("price", TypeName = "decimal(12, 2)")]
    public decimal Price { get; set; }

    [Column("is_available_today")]
    public bool IsAvailableToday { get; set; }

    [Column("image_url")]
    [StringLength(255)]
    public string? ImageUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Item")]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
