using System.ComponentModel.DataAnnotations;

namespace ShieldChecker.DataAccess.Models
{
    public class SharedTestDefinition
    {
        public int ID { get; set; }

        [Required]
        [StringLength(150)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(16)]
        public string MitreTechnique { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        [Display(Name = "Expected alert title (Comma separated if multiple)")]
        public string ExpectedAlertTitle { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Main Test Script")]
        public string ScriptTest { get; set; } = string.Empty;

        [Display(Name = "Prerequisites Script")]
        public string ScriptPrerequisites { get; set; } = string.Empty;

        [Display(Name = "Cleanup Script")]
        public string ScriptCleanup { get; set; } = string.Empty;

        public bool ElevationRequired { get; set; }

        public OperatingSystem OperatingSystem { get; set; }

        public ExecutorSystemType ExecutorSystemType { get; set; }

        public ExecutorUserType ExecutorUserType { get; set; }

        /// <summary>External identifier, e.g. Atomic Red Team GUID, for deduplication.</summary>
        [StringLength(256)]
        public string? ExternalId { get; set; }

        public SharedTestStatus Status { get; set; }

        public UserInfo SubmittedBy { get; set; } = default!;

        public DateTime SubmittedAt { get; set; }

        public UserInfo? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public List<SharedTestConsumption> Consumptions { get; set; } = new();
    }
}
