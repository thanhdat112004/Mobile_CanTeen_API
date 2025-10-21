using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WEB_API_CANTEEN.Models;

[Table("AuditLogs")]
public partial class AuditLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("actor_id")]
    public long? ActorId { get; set; }

    [Required]
    [Column("action")]
    [StringLength(50)]
    public string Action { get; set; } = null!;   // CREATE / UPDATE / DELETE ...

    [Column("entity")]
    [StringLength(50)]
    public string? Entity { get; set; }           // "Item", "Order", ... (cho phép null)

    [Column("entity_id")]
    public long? EntityId { get; set; }

    [Column("detail")]
    [StringLength(1000)]
    public string? Detail { get; set; }           // mô tả chi tiết (nullable)

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(ActorId))]
    public virtual User? Actor { get; set; }
}
