using ShieldChecker.HostService.Core;

namespace ShieldChecker.HostService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("ShieldChecker HostService started.");

                string? apiHostname = _configuration["ShieldCheckerApi:Hostname"];
                string? apiScope = _configuration["ShieldCheckerApi:Scope"];
                string? tenantId = _configuration["AzureAd:TenantId"];
                string? clientId = _configuration["AzureAd:ClientId"];
                string? clientSecret = _configuration["AzureAd:ClientSecret"];

                if (string.IsNullOrWhiteSpace(apiHostname) ||
                    string.IsNullOrWhiteSpace(apiScope) ||
                    string.IsNullOrWhiteSpace(tenantId) ||
                    string.IsNullOrWhiteSpace(clientId) ||
                    string.IsNullOrWhiteSpace(clientSecret))
                {
                    _logger.LogError(
                        "One or more required configuration values are missing. " +
                        "Run with --setup to configure the service.");
                    return;
                }

                string? hyperVAdminUsername = _configuration["HyperV:AdminUsername"];
                string? hyperVAdminPassword = _configuration["HyperV:AdminPassword"];
                bool hyperVEnabled = !string.IsNullOrWhiteSpace(hyperVAdminUsername)
                                  && !string.IsNullOrWhiteSpace(hyperVAdminPassword);

                if (!hyperVEnabled)
                {
                    _logger.LogWarning(
                        "HyperV:AdminUsername or HyperV:AdminPassword is not configured. " +
                        "Scripts will be executed locally instead of inside Hyper-V VMs. " +
                        "Run with --setup to configure Hyper-V credentials.");
                }

                var tokenService = new TokenService(tenantId, clientId, clientSecret);
                var engine = new HostEngine(
                    Environment.MachineName,
                    apiHostname,
                    apiScope,
                    tokenService,
                    _logger);

                // Fetch VM settings once on startup; refresh on each cycle if needed.
                VmSettings? vmSettings = null;
                if (hyperVEnabled)
                {
                    try
                    {
                        vmSettings = await engine.GetVmSettingsAsync(stoppingToken);
                        if (vmSettings == null)
                            _logger.LogWarning("Could not retrieve VM settings from API. Falling back to local execution.");
                        else
                            _logger.LogInformation(
                                "VM settings loaded – WorkerVM: {Cpu} CPUs / {Ram} MB / Windows image: {WinImg} / Linux image: {LinImg} / Storage: {Storage}",
                                vmSettings.WorkerVMCpuCount, vmSettings.WorkerVMMemoryMB,
                                vmSettings.WorkerVMWindowsImage, vmSettings.WorkerVMLinuxImage,
                                vmSettings.VMStoragePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch VM settings. Falling back to local execution.");
                    }
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    string executorOutput = $"INFO: Worker running at: {DateTimeOffset.Now}{Environment.NewLine}";
                    _logger.LogTrace("Worker running at: {Time}", DateTimeOffset.Now);

                    try
                    {
                        var jobDetails = await engine.GetJobDetailsAsync(stoppingToken);

                        if (jobDetails != null)
                        {
                            if (hyperVEnabled && vmSettings != null)
                            {
                                engine.ExecuteJobInVm(jobDetails, vmSettings, hyperVAdminUsername!, hyperVAdminPassword!);
                            }
                            else
                            {
                                engine.ExecuteJobScripts(jobDetails);
                            }

                            string logFilePath = Path.Combine(AppContext.BaseDirectory, "logs", "executor.log");
                            string testOutput = File.Exists(logFilePath) ? File.ReadAllText(logFilePath) : string.Empty;

                            var update = new JobUpdate
                            {
                                ExecutorOutput = executorOutput,
                                TestOutput = testOutput,
                                Status = 2 // WaitingForDetection – the MDE alert detection service will handle final completion
                            };
                            await engine.UpdateJobAsync(update, stoppingToken);
                        }
                        else
                        {
                            _logger.LogTrace("No job found.");
                        }
                    }
                    catch (Exception ex)
                    {
                        var update = new JobUpdate
                        {
                            ExecutorOutput = executorOutput,
                            TestOutput = string.Empty,
                            Status = 7
                        };
                        try { await engine.UpdateJobAsync(update, stoppingToken); } catch { /* best effort */ }
                        _logger.LogError(ex, "Unhandled error during job processing.");
                    }

                    await Task.Delay(30_000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the stopping token is triggered.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Message}", ex.Message);
                Environment.Exit(1);
            }
        }
    }
}

