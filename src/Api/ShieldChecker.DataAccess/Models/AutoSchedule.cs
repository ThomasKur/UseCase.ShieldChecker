using System.ComponentModel.DataAnnotations;

namespace ShieldChecker.DataAccess.Models
{

    public class AutoSchedule
    {
        public int ID { get; set; }
        public required string Name { get; set; }
        public bool Enabled { get; set; }
        [Display(Name = "Next Execution")]
        public DateTime NextExecution { get; set; }
        [Display(Name = "AutoSchedule Type")]
        public AutoScheduleType Type { get; set; }
        [Display(Name = "Select specific Tests to Schedule")]
        public required List<TestDefinition> TestDefinitions { get; set; } = [];
        [Display(Name = "Max number of Tests to select out of the filtered or selected tests per run")]
        public int? FilterRandomCount { get; set; } = 0;
        [Display(Name = "Filter by Operating System")]
        public OperatingSystem? FilterOperatingSystem { get; set; }
        [Display(Name = "Filter based on past Executionresults")]
        public FilterExecution? FilterExecution { get; set; }

    }
}
