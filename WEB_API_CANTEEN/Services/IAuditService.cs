using WEB_API_CANTEEN.Models;

namespace WEB_API_CANTEEN.Services
{
    public interface IAuditService
    {
        void Log(long? actorId, string action, string entity, long? entityId = null, string? detail = null);
    }
}
