using ShieldChecker.DataAccess;
using ShieldChecker.DataAccess.Models;

namespace ShieldChecker.BackendApi.Services
{
    /// <summary>
    /// Writes immutable audit records for all state-changing API operations.
    /// </summary>
    public class AuditService
    {
        private readonly ShieldCheckerContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(ShieldCheckerContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(
            string entityType,
            int? entityId,
            string action,
            string? actorOid,
            string? actorName,
            string? actorUpn,
            string? details = null,
            CancellationToken ct = default)
        {
            var entry = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                ActorOid = actorOid,
                ActorName = actorName,
                ActorUpn = actorUpn,
                Details = details
            };
            _context.AuditLogs.Add(entry);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Audit failures must never surface to callers
                _logger.LogError(ex, "Failed to write audit log for {Action} on {EntityType}/{EntityId}", action, entityType, entityId);
            }
        }
    }
}
