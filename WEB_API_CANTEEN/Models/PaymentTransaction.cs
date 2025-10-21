using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class PaymentTransaction
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public string Method { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? RefCode { get; set; }

    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? ActorId { get; set; }

    public virtual User? Actor { get; set; }

    public virtual Order Order { get; set; } = null!;
}
