using System.Text.Json.Serialization;

namespace ShieldChecker.HostService.Core
{
    /// <summary>
    /// Hyper-V VM configuration settings fetched from the ShieldChecker HostService API.
    /// </summary>
    public class VmSettings
    {
        [JsonPropertyName("workerVMCpuCount")]
        public int WorkerVMCpuCount { get; set; }

        [JsonPropertyName("workerVMMemoryMB")]
        public long WorkerVMMemoryMB { get; set; }

        [JsonPropertyName("workerVMWindowsImage")]
        public string WorkerVMWindowsImage { get; set; } = string.Empty;

        [JsonPropertyName("workerVMLinuxImage")]
        public string WorkerVMLinuxImage { get; set; } = string.Empty;

        [JsonPropertyName("dcVMCpuCount")]
        public int DcVMCpuCount { get; set; }

        [JsonPropertyName("dcVMMemoryMB")]
        public long DcVMMemoryMB { get; set; }

        [JsonPropertyName("dcVMImage")]
        public string DcVMImage { get; set; } = string.Empty;

        [JsonPropertyName("vmStoragePath")]
        public string VMStoragePath { get; set; } = string.Empty;

        [JsonPropertyName("mdeWindowsOnboardingScript")]
        public string MdeWindowsOnboardingScript { get; set; } = string.Empty;

        [JsonPropertyName("mdeLinuxOnboardingScript")]
        public string MdeLinuxOnboardingScript { get; set; } = string.Empty;

        /// <summary>
        /// Returns the VHDX image path for the given operating system.
        /// </summary>
        public string GetImagePathForOs(OperatingSystem os) => os switch
        {
            OperatingSystem.Windows => WorkerVMWindowsImage,
            OperatingSystem.Linux   => WorkerVMLinuxImage,
            _                       => throw new NotSupportedException($"No image configured for OS {os}.")
        };

        /// <summary>
        /// Returns the MDE onboarding script content for the given operating system.
        /// </summary>
        public string GetMdeOnboardingScriptForOs(OperatingSystem os) => os switch
        {
            OperatingSystem.Windows => MdeWindowsOnboardingScript,
            OperatingSystem.Linux   => MdeLinuxOnboardingScript,
            _                       => string.Empty
        };
    }
}
