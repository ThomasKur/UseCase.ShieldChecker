using ShieldChecker.HostService.Core;

namespace ShieldChecker.HostService.Core.Tests
{
    public class TokenServiceTests
    {
        [Fact]
        public void TokenService_Constructor_DoesNotThrow()
        {
            // Verifies that the constructor accepts valid arguments without throwing.
            var svc = new TokenService("tenant-id", "client-id", "client-secret");
            Assert.NotNull(svc);
        }

        [Fact]
        public async Task AcquireTokenAsync_ThrowsForEmptyScope()
        {
            var svc = new TokenService("tenant-id", "client-id", "client-secret");
            // An empty scope should produce an exception from MSAL/HTTP layer.
            await Assert.ThrowsAnyAsync<Exception>(() =>
                svc.AcquireTokenAsync(string.Empty));
        }

        [Fact]
        public async Task AcquireTokenAsync_ThrowsForInvalidTenantCredentials()
        {
            // Deliberately invalid credentials – the HTTP call to AAD should fail.
            var svc = new TokenService("invalid-tenant", "invalid-client", "invalid-secret");
            await Assert.ThrowsAnyAsync<Exception>(() =>
                svc.AcquireTokenAsync("api://some-scope/.default"));
        }
    }

    public class JobUpdateTests
    {
        [Fact]
        public void JobUpdate_Properties_AreSettable()
        {
            var update = new JobUpdate
            {
                ExecutorOutput = "output",
                TestOutput = "test output",
                Status = 2
            };
            Assert.Equal("output", update.ExecutorOutput);
            Assert.Equal("test output", update.TestOutput);
            Assert.Equal(2, update.Status);
        }
    }

    public class TestDefinitionTests
    {
        [Fact]
        public void TestDefinition_DefaultOperatingSystem_IsWindows()
        {
            var td = new TestDefinition();
            Assert.Equal(OperatingSystem.Windows, td.OperatingSystem);
        }
    }

    public class VmSettingsTests
    {
        [Fact]
        public void VmSettings_DefaultValues_AreEmpty()
        {
            var settings = new VmSettings();
            Assert.Equal(0, settings.WorkerVMCpuCount);
            Assert.Equal(0L, settings.WorkerVMMemoryMB);
            Assert.Equal(string.Empty, settings.WorkerVMWindowsImage);
            Assert.Equal(string.Empty, settings.WorkerVMLinuxImage);
            Assert.Equal(string.Empty, settings.VMStoragePath);
        }

        [Fact]
        public void VmSettings_GetImagePathForOs_ReturnsWindowsImage()
        {
            var settings = new VmSettings
            {
                WorkerVMWindowsImage = @"D:\Images\Windows11.vhdx",
                WorkerVMLinuxImage   = @"D:\Images\Ubuntu2404.vhdx"
            };
            Assert.Equal(@"D:\Images\Windows11.vhdx", settings.GetImagePathForOs(OperatingSystem.Windows));
        }

        [Fact]
        public void VmSettings_GetImagePathForOs_ReturnsLinuxImage()
        {
            var settings = new VmSettings
            {
                WorkerVMWindowsImage = @"D:\Images\Windows11.vhdx",
                WorkerVMLinuxImage   = @"D:\Images\Ubuntu2404.vhdx"
            };
            Assert.Equal(@"D:\Images\Ubuntu2404.vhdx", settings.GetImagePathForOs(OperatingSystem.Linux));
        }

        [Fact]
        public void VmSettings_GetImagePathForOs_ThrowsForUnsupportedOs()
        {
            var settings = new VmSettings();
            Assert.Throws<NotSupportedException>(() =>
                settings.GetImagePathForOs((OperatingSystem)99));
        }

        [Fact]
        public void VmSettings_Properties_AreSettable()
        {
            var settings = new VmSettings
            {
                WorkerVMCpuCount     = 4,
                WorkerVMMemoryMB     = 8192,
                WorkerVMWindowsImage = @"C:\Images\win.vhdx",
                WorkerVMLinuxImage   = @"C:\Images\linux.vhdx",
                DcVMCpuCount         = 2,
                DcVMMemoryMB         = 4096,
                DcVMImage            = @"C:\Images\dc.vhdx",
                VMStoragePath        = @"D:\HyperV\VMs"
            };

            Assert.Equal(4, settings.WorkerVMCpuCount);
            Assert.Equal(8192L, settings.WorkerVMMemoryMB);
            Assert.Equal(@"C:\Images\win.vhdx", settings.WorkerVMWindowsImage);
            Assert.Equal(@"C:\Images\linux.vhdx", settings.WorkerVMLinuxImage);
            Assert.Equal(2, settings.DcVMCpuCount);
            Assert.Equal(4096L, settings.DcVMMemoryMB);
            Assert.Equal(@"C:\Images\dc.vhdx", settings.DcVMImage);
            Assert.Equal(@"D:\HyperV\VMs", settings.VMStoragePath);
        }
    }
}
