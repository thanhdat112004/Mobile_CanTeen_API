using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

[Table("Voucher")]
[Index("Code", Name = "UQ__Voucher__357D4CF9C5C2FB47", IsUnique = true)]
public partial class Voucher
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("code")]
    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Column("type")]
    [StringLength(20)]
    public string Type { get; set; } = null!;

    [Column("value", TypeName = "decimal(12, 2)")]
    public decimal Value { get; set; }

    [Column("quota")]
    public int Quota { get; set; }

    [Column("used")]
    public int Used { get; set; }

    [Column("start_at")]
    public DateTime? StartAt { get; set; }

    [Column("end_at")]
    public DateTime? EndAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
