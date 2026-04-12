namespace ShieldChecker.WebApp.Models.Db
{
    public enum AutoScheduleType
    {
        Weekly,
        Monthly,
        Quarterly,
        Daily
    }
    public enum FilterExecution
    {
        None,
        OnlyWhenNoSuccessJobInPastWeek,
        OnlyWhenNoSuccessJobInPastMonth,
        OnlyWhenNoJobInPastWeek,
        OnlyWhenNoJobInPastMonth
    }
    public enum SchedulerType
    {
        Worker,
        DC
    }
    public enum DomainControllerStatus
    {
        NotStarted,
        VMRequested,
        DcProvisioningRequested,
        Initialized,
        ResetRequested,
        Error
    }
    public enum OperatingSystem
    {
        Windows,
        Linux
    }
    public enum ExecutorSystemType
    {
        Worker,
        DomainController
    }
    public enum ExecutorUserType
    {
        System,
        local_admin,
        domain_admin,
        domain_user
    }
    public enum JobStatus
    {
        Queued,
        WaitingForMDE,
        WaitingForDetection,
        ReviewPending,
        ReviewDone,
        Completed,
        Canceled,
        Error,
        AzureSpotEvicted
    }
    public enum JobResult
    {
        Success,
        SuccessWithOtherDetection,
        Failed,
        Undetermined
    }

    public enum SharedTestStatus
    {
        Draft,
        Approved
    }

}
