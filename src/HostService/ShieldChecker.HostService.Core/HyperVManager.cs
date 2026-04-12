using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace ShieldChecker.HostService.Core
{
    /// <summary>
    /// Manages Hyper-V virtual machine lifecycle using PowerShell cmdlets.
    /// All operations run via <c>powershell.exe</c> so no additional NuGet packages
    /// are required – the Hyper-V PowerShell module ships with Windows Server / Hyper-V.
    ///
    /// All external values (paths, VM names, credentials) are passed to PowerShell via
    /// environment variables rather than script-string interpolation, preventing injection attacks.
    /// </summary>
    public class HyperVManager
    {
        private readonly ILogger _logger;

        public HyperVManager(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates a new Hyper-V virtual machine backed by a differencing disk derived
        /// from <paramref name="imagePath"/>.
        /// </summary>
        public void CreateVm(string vmName, string imagePath, int cpuCount, long memoryMB, string storagePath)
        {
            _logger.LogInformation("Creating Hyper-V VM '{VmName}' (CPUs: {Cpu}, RAM: {Ram} MB, image: {Image}).",
                vmName, cpuCount, memoryMB, imagePath);

            long memoryBytes = memoryMB * 1024L * 1024L;

            // All external values are passed via environment variables – never interpolated into the script.
            const string script = @"
$ErrorActionPreference = 'Stop'
$vmName      = $env:SC_VM_NAME
$imagePath   = $env:SC_IMAGE_PATH
$storagePath = $env:SC_STORAGE_PATH
$vhdxPath    = Join-Path $storagePath ($vmName + '.vhdx')
$cpuCount    = [int]$env:SC_CPU_COUNT
$memBytes    = [long]$env:SC_MEMORY_BYTES

if (-not (Test-Path $storagePath)) { New-Item -ItemType Directory -Path $storagePath | Out-Null }
New-VHD -Path $vhdxPath -ParentPath $imagePath -Differencing | Out-Null
New-VM -Name $vmName -NoVHD -Generation 2 -Path $storagePath | Out-Null
Add-VMHardDiskDrive -VMName $vmName -Path $vhdxPath | Out-Null
Set-VMProcessor -VMName $vmName -Count $cpuCount
Set-VMMemory -VMName $vmName -StartupBytes $memBytes
Set-VMFirmware -VMName $vmName -EnableSecureBoot Off
";
            var env = new Dictionary<string, string>
            {
                ["SC_VM_NAME"]      = vmName,
                ["SC_IMAGE_PATH"]   = imagePath,
                ["SC_STORAGE_PATH"] = storagePath,
                ["SC_CPU_COUNT"]    = cpuCount.ToString(),
                ["SC_MEMORY_BYTES"] = memoryBytes.ToString()
            };

            RunPowerShell(script, "create VM", env);
            _logger.LogInformation("VM '{VmName}' created successfully.", vmName);
        }

        /// <summary>
        /// Starts the VM and waits up to <paramref name="timeoutSeconds"/> for the
        /// Hyper-V heartbeat integration component to become available.
        /// </summary>
        public void StartVmAndWait(string vmName, int timeoutSeconds = 300)
        {
            _logger.LogInformation("Starting VM '{VmName}'.", vmName);

            const string script = @"
$ErrorActionPreference = 'Stop'
$vmName      = $env:SC_VM_NAME
$timeoutSecs = [int]$env:SC_TIMEOUT_SECONDS
Start-VM -Name $vmName
$deadline = (Get-Date).AddSeconds($timeoutSecs)
do {
    Start-Sleep -Seconds 5
    $status = (Get-VMIntegrationService -VMName $vmName -Name 'Heartbeat').PrimaryStatusDescription
} while ($status -ne 'OK' -and (Get-Date) -lt $deadline)
if ($status -ne 'OK') { throw 'VM heartbeat not detected within timeout.' }
";
            var env = new Dictionary<string, string>
            {
                ["SC_VM_NAME"]         = vmName,
                ["SC_TIMEOUT_SECONDS"] = timeoutSeconds.ToString()
            };

            RunPowerShell(script, "start VM", env);
            _logger.LogInformation("VM '{VmName}' started and heartbeat confirmed.", vmName);
        }

        /// <summary>
        /// Runs a PowerShell script inside the VM using PowerShell Direct
        /// (<c>Invoke-Command -VMName -FilePath</c>).  The script content is written to a
        /// temporary file whose path is passed via an environment variable, preventing injection.
        /// PowerShell Direct requires Hyper-V integration services to be running in the guest.
        /// </summary>
        public void RunScriptInVm(string vmName, string scriptContent, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                _logger.LogInformation("No script content provided for VM '{VmName}'. Skipping.", vmName);
                return;
            }

            // Write the guest script to a temp file; its path is passed via env var.
            string guestScriptPath = Path.Combine(Path.GetTempPath(), $"sc-guest-{Guid.NewGuid()}.ps1");
            File.WriteAllText(guestScriptPath, scriptContent, Encoding.UTF8);

            try
            {
                // The wrapper uses Invoke-Command -FilePath so the script content is never
                // embedded in the outer script string.
                const string wrapperScript = @"
$ErrorActionPreference = 'Stop'
$vmName     = $env:SC_VM_NAME
$username   = $env:SC_USERNAME
$password   = $env:SC_PASSWORD | ConvertTo-SecureString -AsPlainText -Force
$scriptFile = $env:SC_SCRIPT_FILE
$credential = New-Object System.Management.Automation.PSCredential ($username, $password)
Invoke-Command -VMName $vmName -Credential $credential -FilePath $scriptFile
";
                var env = new Dictionary<string, string>
                {
                    ["SC_VM_NAME"]    = vmName,
                    ["SC_USERNAME"]   = username,
                    ["SC_PASSWORD"]   = password,
                    ["SC_SCRIPT_FILE"] = guestScriptPath
                };

                RunPowerShell(wrapperScript, $"run script in VM '{vmName}'", env);
            }
            finally
            {
                if (File.Exists(guestScriptPath))
                    File.Delete(guestScriptPath);
            }
        }

        /// <summary>
        /// Forcefully stops the VM, removes it from Hyper-V, and deletes its differencing disk.
        /// </summary>
        public void RemoveVm(string vmName, string storagePath)
        {
            _logger.LogInformation("Removing Hyper-V VM '{VmName}'.", vmName);

            const string script = @"
$ErrorActionPreference = 'Stop'
$vmName      = $env:SC_VM_NAME
$storagePath = $env:SC_STORAGE_PATH
$vhdxPath    = Join-Path $storagePath ($vmName + '.vhdx')
if (Get-VM -Name $vmName -ErrorAction SilentlyContinue) {
    Stop-VM -Name $vmName -Force -TurnOff
    Remove-VM -Name $vmName -Force
}
if (Test-Path $vhdxPath) { Remove-Item $vhdxPath -Force }
";
            var env = new Dictionary<string, string>
            {
                ["SC_VM_NAME"]      = vmName,
                ["SC_STORAGE_PATH"] = storagePath
            };

            RunPowerShell(script, "remove VM", env);
            _logger.LogInformation("VM '{VmName}' removed.", vmName);
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private void RunPowerShell(string script, string operationName, Dictionary<string, string>? envVars = null)
        {
            string tempScript = Path.Combine(Path.GetTempPath(), $"sc-hyperv-{Guid.NewGuid()}.ps1");
            File.WriteAllText(tempScript, script, Encoding.UTF8);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NonInteractive -ExecutionPolicy Bypass -File \"{tempScript}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (envVars != null)
                {
                    foreach (var (key, value) in envVars)
                        psi.Environment[key] = value;
                }

                using var process = new Process { StartInfo = psi };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool exited = process.WaitForExit(TimeSpan.FromMinutes(10));

                if (!exited)
                {
                    process.Kill();
                    // Flush async readers after killing
                    process.WaitForExit();
                    throw new TimeoutException($"Hyper-V operation '{operationName}' timed out after 10 minutes.");
                }

                // Second WaitForExit() (no timeout) ensures async output readers have flushed.
                process.WaitForExit();

                if (stdout.Length > 0)
                    _logger.LogInformation("HyperV [{Op}] STDOUT: {Out}", operationName, stdout);
                if (stderr.Length > 0)
                    _logger.LogWarning("HyperV [{Op}] STDERR: {Err}", operationName, stderr);

                if (process.ExitCode != 0)
                    throw new InvalidOperationException(
                        $"Hyper-V operation '{operationName}' failed (exit code {process.ExitCode}). {stderr}");
            }
            finally
            {
                if (File.Exists(tempScript))
                    File.Delete(tempScript);
            }
        }
    }
}
