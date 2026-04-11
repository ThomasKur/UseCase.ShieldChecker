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
}
