using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

[Table("AuditLog")]
public partial class AuditLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("actor_id")]
    public long? ActorId { get; set; }

    [Column("action")]
    [StringLength(100)]
    public string Action { get; set; } = null!;

    [Column("entity")]
    [StringLength(100)]
    public string Entity { get; set; } = null!;

    [Column("entity_id")]
    public long? EntityId { get; set; }

    [Column("at")]
    public DateTime At { get; set; }

    [ForeignKey("ActorId")]
    [InverseProperty("AuditLogs")]
    public virtual User? Actor { get; set; }
}
