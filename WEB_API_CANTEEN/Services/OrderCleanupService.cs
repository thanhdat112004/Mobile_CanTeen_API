using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Services
{
    public class OrderCleanupOptions
    {
        public int CancelAfterMinutes { get; set; } = 15;  // quá thời gian này thì hủy
        public int IntervalSeconds { get; set; } = 60;   // kiểm tra mỗi bao lâu
    }

    public class OrderCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OrderCleanupService> _logger;
        private readonly OrderCleanupOptions _opt;

        public OrderCleanupService(
            IServiceScopeFactory scopeFactory,
            IOptions<OrderCleanupOptions> opt,
            ILogger<OrderCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _opt = opt.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrderCleanupService started: every {sec}s, cancel after {min} minutes.",
                _opt.IntervalSeconds, _opt.CancelAfterMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try { await DoCleanup(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "OrderCleanupService error"); }

                await Task.Delay(TimeSpan.FromSeconds(_opt.IntervalSeconds), stoppingToken);
            }
        }

        private async Task DoCleanup(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<SmartCanteenDbContext>();

            var thresholdUtc = DateTime.UtcNow.AddMinutes(-_opt.CancelAfterMinutes);

            var stale = await ctx.Orders
                .Where(o => o.Status == "PENDING"
                         && o.PaymentStatus == "UNPAID"
                         && o.CreatedAt <= thresholdUtc)
                .ToListAsync(ct);

            if (stale.Count == 0) return;

            foreach (var o in stale)
            {
                o.Status = "CANCELLED";

                ctx.UserNotifications.Add(new UserNotification
                {
                    UserId = o.UserId,
                    Title = $"Đơn #{o.Id} đã bị hủy tự động",
                    Body = $"Đơn PENDING quá {_opt.CancelAfterMinutes} phút không thanh toán.",
                    Type = "ORDER_STATUS",
                    ReferenceId = o.Id,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                ctx.AuditLogs.Add(new AuditLog
                {
                    ActorId = null,
                    Action = "AUTO_CANCEL",
                    Entity = "Order",
                    EntityId = o.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await ctx.SaveChangesAsync(ct);
        }
    }
}
