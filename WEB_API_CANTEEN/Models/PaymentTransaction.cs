using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

public partial class PaymentTransaction
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("order_id")]
    public long OrderId { get; set; }

    [Column("method")]
    [StringLength(20)]
    public string Method { get; set; } = null!;

    [Column("action")]
    [StringLength(20)]
    public string Action { get; set; } = null!;

    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Column("ref_code")]
    [StringLength(100)]
    public string? RefCode { get; set; }

    [Column("amount", TypeName = "decimal(12, 2)")]
    public decimal Amount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("actor_id")]
    public long? ActorId { get; set; }

    [ForeignKey("ActorId")]
    [InverseProperty("PaymentTransactions")]
    public virtual User? Actor { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("PaymentTransactions")]
    public virtual Order Order { get; set; } = null!;
}
