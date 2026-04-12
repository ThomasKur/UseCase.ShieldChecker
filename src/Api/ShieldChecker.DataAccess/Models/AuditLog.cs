namespace ShieldChecker.DataAccess.Models
{
    /// <summary>
    /// Immutable audit record capturing who did what and when.
    /// Written by API controllers for all state-changing operations.
    /// </summary>
    public class AuditLog
    {
        public int ID { get; set; }

        /// <summary>UTC timestamp of the action.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Object type affected (e.g. "TestDefinition", "TestJob", "Settings").</summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>Numeric ID of the affected entity, if applicable.</summary>
        public int? EntityId { get; set; }

        /// <summary>Action performed (e.g. "Create", "Update", "Delete", "Cancel", "Approve").</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Entra Object ID of the user who triggered the action.</summary>
        public string? ActorOid { get; set; }

        /// <summary>Display name of the actor at the time of the action.</summary>
        public string? ActorName { get; set; }

        /// <summary>UPN of the actor at the time of the action.</summary>
        public string? ActorUpn { get; set; }

        /// <summary>Optional free-text description or payload summary.</summary>
        public string? Details { get; set; }
    }
}
