using System.CodeDom.Compiler;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ShieldChecker.WebApp.Models.Db
{
    
    public class TestDefinition
    {
        public int ID { get; set; }
        [Required]
        [StringLength(150)]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;
        [StringLength(16)]
        public string MitreTechnique { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime Created { get; set; }
        [Display(Name = "Created by")]
        public UserInfo CreatedBy { get; set; }
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime Modified { get; set; }
        [Display(Name = "Modified by")]
        public UserInfo ModifiedBy { get; set; }
        [Required]
        [StringLength(256)]
        [Display(Name = "Expected alert title (Comma seperated if multiple)")]
        public string ExpectedAlertTitle { get; set; } = string.Empty;
        public bool? Enabled { get; set; }
        public bool? ReadOnly { get; set; }
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
        public List<TestJob> TestJobs { get; set; } = new List<TestJob>();
        public List<AutoSchedule> AutoSchedules { get; } = new List<AutoSchedule>();

        /// <summary>Links this test to a shared library entry it was copied from.</summary>
        public int? SharedLibrarySourceId { get; set; }
        public SharedTestDefinition? SharedLibrarySource { get; set; }
    }

}
