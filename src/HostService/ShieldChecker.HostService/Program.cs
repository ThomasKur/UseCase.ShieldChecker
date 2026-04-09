using ShieldChecker.HostService;
using ShieldChecker.HostService.Core;

// Run the interactive setup wizard when --setup is the first argument.
if (args.Length > 0 && args[0].Equals("--setup", StringComparison.OrdinalIgnoreCase))
{
    return await SetupWizard.RunAsync();
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "ShieldCheckerHostService";
});
builder.Services.AddSystemd();

builder.Services.AddHostedService<Worker>()
    .AddLogging(config =>
    {
        config.AddDebug();
        config.AddConsole();

        string logFilePath = Path.Combine(AppContext.BaseDirectory, "logs", "executor.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
        config.AddProvider(new FileLoggerProvider(logFilePath));
    });

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var host = builder.Build();
host.Run();

return 0;
