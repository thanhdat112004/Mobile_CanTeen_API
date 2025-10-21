using System;
using Microsoft.EntityFrameworkCore;

namespace WEB_API_CANTEEN.Models;

public partial class SmartCanteenDbContext : DbContext
{
    public SmartCanteenDbContext() { }

    public SmartCanteenDbContext(DbContextOptions<SmartCanteenDbContext> options)
        : base(options) { }

    public virtual DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public virtual DbSet<Category> Categories { get; set; } = null!;
    public virtual DbSet<Item> Items { get; set; } = null!;
    public virtual DbSet<Order> Orders { get; set; } = null!;
    public virtual DbSet<OrderItem> OrderItems { get; set; } = null!;
    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;
    public virtual DbSet<PointsLedger> PointsLedgers { get; set; } = null!;
    public virtual DbSet<User> Users { get; set; } = null!;
    public virtual DbSet<UserNotification> UserNotifications { get; set; } = null!;
    public virtual DbSet<Voucher> Vouchers { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148.
        => optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=SmartCanteen;Trusted_Connection=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ===== AuditLogs =====
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");

            entity.HasKey(e => e.Id).HasName("PK_AuditLogs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.Action).HasMaxLength(50).HasColumnName("action");
            entity.Property(e => e.Entity).HasMaxLength(50).HasColumnName("entity");
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.Detail).HasMaxLength(1000).HasColumnName("detail");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");

            entity.HasOne(d => d.Actor)
                  .WithMany(p => p.AuditLogs)
                  .HasForeignKey(d => d.ActorId)
                  .HasConstraintName("FK_AuditLogs_Users");
        });

        // ===== Categories =====
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");

            entity.HasKey(e => e.Id).HasName("PK_Categories");
            entity.HasIndex(e => e.Name, "UQ_Categories_Name").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.SortOrder).HasColumnName("sort_order");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        });

        // ===== Items =====
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("Items");

            entity.HasKey(e => e.Id).HasName("PK_Items");
            entity.HasIndex(e => e.CategoryId, "IX_Items_CategoryId");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasMaxLength(120).HasColumnName("name");
            entity.Property(e => e.Price).HasColumnType("decimal(12, 2)").HasColumnName("price");
            entity.Property(e => e.ImageUrl).HasMaxLength(255).HasColumnName("image_url");
            entity.Property(e => e.IsAvailableToday).HasDefaultValue(true).HasColumnName("is_available_today");
            entity.Property(e => e.Category).HasMaxLength(50).HasColumnName("category");       // string (giữ tương thích)
            entity.Property(e => e.CategoryId).HasColumnName("category_id");                   // FK mới
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");

            entity.HasOne(d => d.CategoryNavigation)
                  .WithMany(p => p.Items)
                  .HasForeignKey(d => d.CategoryId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_Items_Categories");
        });

        // ===== Orders =====
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");

            entity.HasKey(e => e.Id).HasName("PK_Orders");
            entity.HasIndex(e => e.UserId, "IX_Orders_UserId");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status");
            entity.Property(e => e.PaymentStatus).HasMaxLength(20).HasDefaultValue("UNPAID").HasColumnName("payment_status");
            entity.Property(e => e.PaymentMethod).HasMaxLength(20).HasColumnName("payment_method");
            entity.Property(e => e.EtaMinutes).HasColumnName("eta_minutes");
            entity.Property(e => e.Note).HasMaxLength(255).HasColumnName("note");
            entity.Property(e => e.Total).HasColumnType("decimal(12, 2)").HasColumnName("total");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
            entity.Property(e => e.PaidAt).HasColumnName("paid_at");
            entity.Property(e => e.PaymentRef).HasMaxLength(100).HasColumnName("payment_ref");

            entity.HasOne(d => d.User)
                  .WithMany(p => p.Orders)
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_Orders_Users");
        });

        // ===== OrderItems =====
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");

            entity.HasKey(e => e.Id).HasName("PK_OrderItems");
            entity.HasIndex(e => e.OrderId, "IX_OrderItems_OrderId");
            entity.HasIndex(e => e.ItemId, "IX_OrderItems_ItemId");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.Qty).HasColumnName("qty");
            entity.Property(e => e.Note).HasMaxLength(255).HasColumnName("note");

            entity.HasOne(d => d.Order)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(d => d.OrderId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_OrderItems_Orders");

            entity.HasOne(d => d.Item)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(d => d.ItemId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_OrderItems_Items");
        });

        // ===== PaymentTransactions =====
        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("PaymentTransactions");

            entity.HasKey(e => e.Id).HasName("PK_PaymentTransactions");
            entity.HasIndex(e => e.OrderId, "IX_PayTrans_OrderId");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ActorId).HasColumnName("actor_id");
            entity.Property(e => e.Method).HasMaxLength(20).HasColumnName("method");
            entity.Property(e => e.Action).HasMaxLength(20).HasColumnName("action");
            entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status");
            entity.Property(e => e.RefCode).HasMaxLength(100).HasColumnName("ref_code");
            entity.Property(e => e.Amount).HasColumnType("decimal(12, 2)").HasColumnName("amount");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");

            entity.HasOne(d => d.Order)
                  .WithMany(p => p.PaymentTransactions)
                  .HasForeignKey(d => d.OrderId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_PayTrans_Orders");

            entity.HasOne(d => d.Actor)
                  .WithMany(p => p.PaymentTransactions)
                  .HasForeignKey(d => d.ActorId)
                  .HasConstraintName("FK_PayTrans_Users");
        });

        // ===== PointsLedger =====
        modelBuilder.Entity<PointsLedger>(entity =>
        {
            entity.ToTable("PointsLedger");

            entity.HasKey(e => e.Id).HasName("PK_PointsLedger");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Points).HasColumnName("points");
            entity.Property(e => e.Delta).HasColumnName("delta"); // nếu bảng có, giữ lại; nếu không có có thể bỏ
            entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");

            entity.HasOne(d => d.User)
                  .WithMany(p => p.PointsLedgers)
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_PointsLedger_Users");

            entity.HasOne(d => d.Order)
                  .WithMany(p => p.PointsLedgers)
                  .HasForeignKey(d => d.OrderId)
                  .HasConstraintName("FK_PointsLedger_Orders");
        });

        // ===== Users =====
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.Id).HasName("PK_Users");
            entity.HasIndex(e => e.Username, "UQ_Users_Username").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasMaxLength(120).HasColumnName("name");
            entity.Property(e => e.Username).HasMaxLength(100).HasColumnName("username");
            entity.Property(e => e.PasswordHash).HasMaxLength(255).HasColumnName("password_hash");
            entity.Property(e => e.Mssv).HasMaxLength(30).HasColumnName("mssv");
            entity.Property(e => e.Class).HasMaxLength(30).HasColumnName("class");
            entity.Property(e => e.Phone).HasMaxLength(20).HasColumnName("phone");
            entity.Property(e => e.Role).HasMaxLength(20).HasColumnName("role");
            entity.Property(e => e.Allergies).HasMaxLength(255).HasColumnName("allergies");
            entity.Property(e => e.Preferences).HasMaxLength(255).HasColumnName("preferences");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        });

        // ===== UserNotifications =====
        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.ToTable("UserNotifications");

            entity.HasKey(e => e.Id).HasName("PK_UserNotifications");
            entity.HasIndex(e => e.UserId, "IX_UserNoti_UserId");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Title).HasMaxLength(200).HasColumnName("title");
            entity.Property(e => e.Body).HasMaxLength(500).HasColumnName("body");
            entity.Property(e => e.Type).HasMaxLength(50).HasColumnName("type");
            entity.Property(e => e.ReferenceId).HasColumnName("reference_id");
            entity.Property(e => e.IsRead).HasColumnName("is_read");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");

            entity.HasOne(d => d.User)
                  .WithMany(p => p.UserNotifications)
                  .HasForeignKey(d => d.UserId)
                  .OnDelete(DeleteBehavior.ClientSetNull)
                  .HasConstraintName("FK_UserNoti_Users");
        });

        // ===== Voucher =====
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.ToTable("Voucher");

            entity.HasKey(e => e.Id).HasName("PK_Voucher");
            entity.HasIndex(e => e.Code, "UQ_Voucher_Code").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasMaxLength(50).HasColumnName("code");
            entity.Property(e => e.Type).HasMaxLength(20).HasColumnName("type");
            entity.Property(e => e.Value).HasColumnType("decimal(12, 2)").HasColumnName("value");
            entity.Property(e => e.Quota).HasColumnName("quota");
            entity.Property(e => e.Used).HasColumnName("used");
            entity.Property(e => e.StartAt).HasColumnName("start_at");
            entity.Property(e => e.EndAt).HasColumnName("end_at");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())").HasColumnName("created_at");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
