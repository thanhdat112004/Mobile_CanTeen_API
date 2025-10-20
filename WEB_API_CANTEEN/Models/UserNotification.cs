using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

public partial class UserNotification
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("title")]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Column("body")]
    [StringLength(500)]
    public string Body { get; set; } = null!;

    [Column("type")]
    [StringLength(50)]
    public string Type { get; set; } = null!;

    [Column("reference_id")]
    public long? ReferenceId { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserNotifications")]
    public virtual User User { get; set; } = null!;
}
