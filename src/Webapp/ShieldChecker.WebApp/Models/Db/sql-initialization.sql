
/*Create or Update the SystemStatus */

IF NOT EXISTS (SELECT * FROM SystemStatus WHERE ID = 1)
    INSERT INTO SystemStatus (ID, IsFirstRunCompleted, DomainControllerStatus,DomainControllerLog, WebAppVersion)
    VALUES (1, 0, 0,'', '1.0.0');
ELSE
    UPDATE SystemStatus
    SET 
        WebAppVersion = '1.0.0'
    WHERE ID = 1;

/* Create Settings Entry */
IF NOT EXISTS (SELECT * FROM Settings WHERE ID = 1)
    INSERT INTO Settings (ID, MaxWorkerCount, JobTimeout,JobReview, WorkerVMCpuCount, WorkerVMMemoryMB, DcVMCpuCount, DcVMMemoryMB, VMStoragePath, DcVMImage, WorkerVMWindowsImage, WorkerVMLinuxImage, DomainFQDN, DomainControllerName, MDEWindowsOnboardingScript, MDELinuxOnboardingScript)
    VALUES (1, 5, 120,0, 2, 4096, 2, 4096, 'C:\HyperV\VMs', '', '', '', '_DomainFQDN_', 'dc01', '', '');

/* Create System User Info */
IF NOT EXISTS (SELECT * FROM UserInfo WHERE Id = '00000000-0000-0000-0000-000000000000')
    INSERT INTO UserInfo (Id, DisplayName, UserPrincipalName)
    VALUES ('00000000-0000-0000-0000-000000000000', 'SYSTEM', 'SYSTEM');
