using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class Order
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string Status { get; set; } = null!;

    public string PaymentStatus { get; set; } = null!;

    public string? PaymentMethod { get; set; }

    public string? PaymentRef { get; set; }

    public decimal Total { get; set; }

    public int? EtaMinutes { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();

    public virtual User User { get; set; } = null!;
}
