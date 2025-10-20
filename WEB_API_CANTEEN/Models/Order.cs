using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

public partial class Order
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("total", TypeName = "decimal(12, 2)")]
    public decimal Total { get; set; }

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("payment_method")]
    [StringLength(20)]
    public string? PaymentMethod { get; set; }

    [Column("payment_status")]
    [StringLength(20)]
    public string PaymentStatus { get; set; } = null!;

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("payment_ref")]
    [StringLength(100)]
    public string? PaymentRef { get; set; }

    [Column("eta_minutes")]
    public int? EtaMinutes { get; set; }

    [Column("note")]
    [StringLength(255)]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Order")]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    [InverseProperty("Order")]
    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    [InverseProperty("Order")]
    public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();

    [ForeignKey("UserId")]
    [InverseProperty("Orders")]
    public virtual User User { get; set; } = null!;
}
