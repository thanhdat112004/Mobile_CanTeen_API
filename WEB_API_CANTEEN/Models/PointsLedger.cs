using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

[Table("PointsLedger")]
public partial class PointsLedger
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("delta")]
    public int Delta { get; set; }

    [Column("reason")]
    [StringLength(255)]
    public string? Reason { get; set; }

    [Column("order_id")]
    public long? OrderId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("PointsLedgers")]
    public virtual Order? Order { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("PointsLedgers")]
    public virtual User User { get; set; } = null!;
}
