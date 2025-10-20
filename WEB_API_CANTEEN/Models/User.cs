using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

[Index("Username", Name = "UQ__Users__F3DBC57239E7CA57", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    [StringLength(120)]
    public string Name { get; set; } = null!;

    [Column("username")]
    [StringLength(100)]
    public string Username { get; set; } = null!;

    [Column("password_hash")]
    [StringLength(255)]
    public string? PasswordHash { get; set; }

    [Column("mssv")]
    [StringLength(30)]
    public string? Mssv { get; set; }

    [Column("class")]
    [StringLength(30)]
    public string? Class { get; set; }

    [Column("phone")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Column("role")]
    [StringLength(20)]
    public string Role { get; set; } = null!;

    [Column("allergies")]
    [StringLength(255)]
    public string? Allergies { get; set; }

    [Column("preferences")]
    [StringLength(255)]
    public string? Preferences { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Actor")]
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    [InverseProperty("User")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    [InverseProperty("Actor")]
    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    [InverseProperty("User")]
    public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();

    [InverseProperty("User")]
    public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
}
