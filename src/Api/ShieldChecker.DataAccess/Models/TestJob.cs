namespace ShieldChecker.DataAccess.Models
{
    public class TestJob
    {
        public int ID { get; set; }
        public int UseCaseID { get; set; }
        public TestDefinition UseCase { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public DateTime? WorkerStart { get; set; }
        public DateTime? WorkerEnd { get; set; }
        public JobStatus Status { get; set; }
        public JobResult Result { get; set; }
        public string? WorkerName { get; set; }
        public string? WorkerIP { get; set; }
        public string? WorkerRemoteIP { get; set; }
        public string? DefenderMachineId { get; set; }
        public string? TestUser { get; set; }
        public string? TestOutput { get; set; }
        public string? SchedulerLog { get; set; }
        public string? DetectedAlerts { get; set; }
        public string? ReviewResult { get; set; }
    }
    
}
