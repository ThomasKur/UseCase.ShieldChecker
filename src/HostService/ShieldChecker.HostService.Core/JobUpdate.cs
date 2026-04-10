namespace ShieldChecker.HostService.Core
{
    public class JobUpdate
    {
        public int? Status { get; set; }
        public required string TestOutput { get; set; }
        public required string ExecutorOutput { get; set; }
    }
}
