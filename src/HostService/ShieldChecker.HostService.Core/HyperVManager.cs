using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace ShieldChecker.HostService.Core
{
    /// <summary>
    /// Manages Hyper-V virtual machine lifecycle using PowerShell cmdlets.
    /// All operations run via <c>powershell.exe</c> so no additional NuGet packages
    /// are required – the Hyper-V PowerShell module ships with Windows Server / Hyper-V.
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
        /// <param name="vmName">Unique name for the new VM.</param>
        /// <param name="imagePath">Full path to the gold-image VHDX file.</param>
        /// <param name="cpuCount">Number of virtual processors to assign.</param>
        /// <param name="memoryMB">Startup memory in megabytes.</param>
        /// <param name="storagePath">Directory where the differencing VHD will be stored.</param>
        public void CreateVm(string vmName, string imagePath, int cpuCount, long memoryMB, string storagePath)
        {
            _logger.LogInformation("Creating Hyper-V VM '{VmName}' (CPUs: {Cpu}, RAM: {Ram} MB, image: {Image}).",
                vmName, cpuCount, memoryMB, imagePath);

            string vhdxPath = Path.Combine(storagePath, $"{vmName}.vhdx");
            long memoryBytes = memoryMB * 1024L * 1024L;

            string script = $@"
$ErrorActionPreference = 'Stop'
if (-not (Test-Path '{storagePath}')) {{ New-Item -ItemType Directory -Path '{storagePath}' | Out-Null }}
New-VHD -Path '{vhdxPath}' -ParentPath '{imagePath}' -Differencing | Out-Null
New-VM -Name '{vmName}' -NoVHD -Generation 2 -Path '{storagePath}' | Out-Null
Add-VMHardDiskDrive -VMName '{vmName}' -Path '{vhdxPath}' | Out-Null
Set-VMProcessor -VMName '{vmName}' -Count {cpuCount}
Set-VMMemory -VMName '{vmName}' -StartupBytes {memoryBytes}
Set-VMFirmware -VMName '{vmName}' -EnableSecureBoot Off
";
            RunPowerShell(script, "create VM");
            _logger.LogInformation("VM '{VmName}' created successfully.", vmName);
        }

        /// <summary>
        /// Starts the VM and waits up to <paramref name="timeoutSeconds"/> for the
        /// Hyper-V heartbeat integration component to become available.
        /// </summary>
        public void StartVmAndWait(string vmName, int timeoutSeconds = 300)
        {
            _logger.LogInformation("Starting VM '{VmName}'.", vmName);

            string script = $@"
$ErrorActionPreference = 'Stop'
Start-VM -Name '{vmName}'
$deadline = (Get-Date).AddSeconds({timeoutSeconds})
do {{
    Start-Sleep -Seconds 5
    $status = (Get-VMIntegrationService -VMName '{vmName}' -Name 'Heartbeat').PrimaryStatusDescription
}} while ($status -ne 'OK' -and (Get-Date) -lt $deadline)
if ($status -ne 'OK') {{ throw ""VM '{vmName}' heartbeat not detected within {timeoutSeconds} seconds."" }}
";
            RunPowerShell(script, "start VM");
            _logger.LogInformation("VM '{VmName}' started and heartbeat confirmed.", vmName);
        }

        /// <summary>
        /// Runs a PowerShell script block inside the VM using PowerShell Direct
        /// (<c>Invoke-Command -VMName</c>).  PowerShell Direct requires Hyper-V integration
        /// services to be running in the guest and the caller to have local administrator
        /// rights on the Hyper-V host.
        /// </summary>
        /// <param name="vmName">Target VM name.</param>
        /// <param name="scriptContent">PowerShell script content to execute in the guest.</param>
        /// <param name="username">Guest local administrator username.</param>
        /// <param name="password">Guest local administrator password.</param>
        public void RunScriptInVm(string vmName, string scriptContent, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                _logger.LogInformation("No script content provided for VM '{VmName}'. Skipping.", vmName);
                return;
            }

            // Escape single quotes in embedded content for PowerShell here-string
            string escapedScript = scriptContent.Replace("'", "''");
            string escapedPassword = password.Replace("'", "''");

            string script = $@"
$ErrorActionPreference = 'Stop'
$securePassword = ConvertTo-SecureString '{escapedPassword}' -AsPlainText -Force
$credential = New-Object System.Management.Automation.PSCredential ('{username}', $securePassword)
$scriptBlock = [ScriptBlock]::Create('{escapedScript}')
Invoke-Command -VMName '{vmName}' -Credential $credential -ScriptBlock $scriptBlock
";
            RunPowerShell(script, $"run script in VM '{vmName}'");
        }

        /// <summary>
        /// Forcefully stops the VM, removes it from Hyper-V, and deletes its differencing disk.
        /// </summary>
        public void RemoveVm(string vmName, string storagePath)
        {
            _logger.LogInformation("Removing Hyper-V VM '{VmName}'.", vmName);

            string vhdxPath = Path.Combine(storagePath, $"{vmName}.vhdx");
            string script = $@"
$ErrorActionPreference = 'Stop'
if (Get-VM -Name '{vmName}' -ErrorAction SilentlyContinue) {{
    Stop-VM -Name '{vmName}' -Force -TurnOff
    Remove-VM -Name '{vmName}' -Force
}}
if (Test-Path '{vhdxPath}') {{ Remove-Item '{vhdxPath}' -Force }}
";
            RunPowerShell(script, "remove VM");
            _logger.LogInformation("VM '{VmName}' removed.", vmName);
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private void RunPowerShell(string script, string operationName)
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

                using var process = new Process { StartInfo = psi };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit(new TimeSpan(0, 10, 0));

                if (!process.HasExited)
                {
                    process.Kill();
                    throw new TimeoutException($"Hyper-V operation '{operationName}' timed out after 10 minutes.");
                }

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
