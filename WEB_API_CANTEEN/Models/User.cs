using System;
using System.Collections.Generic;

namespace WEB_API_CANTEEN.Models;

public partial class User
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string? Mssv { get; set; }

    public string? Class { get; set; }

    public string? Phone { get; set; }

    public string Role { get; set; } = null!;

    public string? Allergies { get; set; }

    public string? Preferences { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    // ❌ BỎ: public virtual ICollection<AuditLog1> AuditLog1s { get; set; } = new List<AuditLog1>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();

    public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
}
