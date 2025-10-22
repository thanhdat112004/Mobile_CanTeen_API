using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class AuditLog
{
    public long Id { get; set; }

    public long? ActorId { get; set; }

    public string Action { get; set; } = null!;

    public string? Entity { get; set; }

    public long? EntityId { get; set; }

    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? Actor { get; set; }
}
