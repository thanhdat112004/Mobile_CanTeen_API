using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Services
{
    public class AuditService : IAuditService
    {
        private readonly SmartCanteenDbContext _ctx;
        public AuditService(SmartCanteenDbContext ctx) => _ctx = ctx;

        public void Log(long? actorId, string action, string entity, long? entityId = null, string? detail = null)
        {
            _ctx.AuditLogs.Add(new AuditLog
            {
                ActorId = actorId,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                CreatedAt = DateTime.UtcNow
                // Lưu ý: model AuditLog của bạn hiện không có cột Detail.
            });
            _ctx.SaveChanges();
        }
    }
}
