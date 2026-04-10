using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ShieldChecker.HostService.Core
{
    /// <summary>
    /// Core engine that polls the ShieldChecker API for jobs and executes them.
    /// Authenticates with the API using the OAuth 2.0 client credentials flow.
    /// </summary>
    public class HostEngine
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly ILogger _logger;
        private readonly string _workerName;
        private readonly string _shieldCheckerApiHostname;
        private readonly TokenService _tokenService;
        private readonly string _shieldCheckerApiScope;

        public HostEngine(
            string workerName,
            string shieldCheckerApiHostname,
            string shieldCheckerApiScope,
            TokenService tokenService,
            ILogger logger)
        {
            _workerName = workerName;
            _shieldCheckerApiHostname = shieldCheckerApiHostname;
            _shieldCheckerApiScope = shieldCheckerApiScope;
            _tokenService = tokenService;
            _logger = logger;
        }

        private async Task<string> GetBearerTokenAsync(CancellationToken ct = default)
        {
            return await _tokenService.AcquireTokenAsync(_shieldCheckerApiScope, ct);
        }

        /// <summary>
        /// Fetches the next pending job from the ShieldChecker API.
        /// Returns null if no job is available or if the domain controller is not yet ready.
        /// </summary>
        public async Task<TestDefinition?> GetJobDetailsAsync(CancellationToken ct = default)
        {
            string requestUrl = $"https://{_shieldCheckerApiHostname}/api/Job?workername={_workerName}";
            _logger.LogTrace("Fetching job details from URL: {Url}", requestUrl);

            string token = await GetBearerTokenAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                string content = await response.Content.ReadAsStringAsync(ct);
                if (content.Contains("Domain Controller is not yet ready, skip processing jobs"))
                {
                    _logger.LogInformation("Domain Controller is not yet ready, skipping job processing.");
                    return null;
                }
                response.EnsureSuccessStatusCode();
            }

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("Job details fetched successfully.");
            return JsonSerializer.Deserialize<TestDefinition>(json);
        }

        /// <summary>
        /// Updates the status and output of a job in the ShieldChecker API.
        /// </summary>
        public async Task UpdateJobAsync(JobUpdate update, CancellationToken ct = default)
        {
            string requestUrl = $"https://{_shieldCheckerApiHostname}/api/JobUpdater?workername={_workerName}";
            _logger.LogInformation("Updating job status at: {Url}", requestUrl);

            string token = await GetBearerTokenAsync(ct);
            var jsonContent = JsonContent.Create(update);
            await jsonContent.LoadIntoBufferAsync();

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Executes the scripts defined in the job in the order: Prerequisites → Test → Cleanup.
        /// </summary>
        public void ExecuteJobScripts(TestDefinition jobDefinition)
        {
            if (jobDefinition == null)
                throw new ArgumentNullException(nameof(jobDefinition));

            _logger.LogInformation("Starting execution of job: {Name}", jobDefinition.Name);

            string shell, scriptExtension;
            switch (jobDefinition.OperatingSystem)
            {
                case OperatingSystem.Windows:
                    shell = "powershell.exe";
                    scriptExtension = ".ps1";
                    break;
                case OperatingSystem.Linux:
                    shell = "pwsh";
                    scriptExtension = ".ps1";
                    break;
                default:
                    throw new NotSupportedException("Unsupported operating system.");
            }

            _logger.LogInformation("Executing prerequisites script...");
            ExecuteScript(shell, jobDefinition.ScriptPrerequisites, scriptExtension,
                jobDefinition.Username, jobDefinition.Password, jobDefinition.Domain);

            _logger.LogInformation("Executing test script...");
            ExecuteScript(shell, jobDefinition.ScriptTest, scriptExtension,
                jobDefinition.Username, jobDefinition.Password, jobDefinition.Domain);

            _logger.LogInformation("Executing cleanup script...");
            ExecuteScript(shell, jobDefinition.ScriptCleanup, scriptExtension,
                jobDefinition.Username, jobDefinition.Password, jobDefinition.Domain);

            _logger.LogInformation("Job execution completed successfully.");
        }

        private void ExecuteScript(string shell, string scriptContent, string scriptExtension,
            string? username = null, string? password = null, string? domain = null)
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                _logger.LogInformation("No script content provided. Skipping execution.");
                return;
            }

            string tempScriptPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + scriptExtension);
            if (isWindows)
            {
                if (!System.IO.Directory.Exists("c:\\TestEngine"))
                    System.IO.Directory.CreateDirectory("c:\\TestEngine");
                tempScriptPath = System.IO.Path.Combine("c:\\TestEngine", Guid.NewGuid() + scriptExtension);
            }
            System.IO.File.WriteAllText(tempScriptPath, scriptContent);
            _logger.LogInformation("Temporary script file created at: {Path}", tempScriptPath);

            string scriptArg = $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"";

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = scriptArg,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (isWindows && !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    processStartInfo.UserName = username;
                    processStartInfo.Password = ConvertToSecureString(password);
                    processStartInfo.Domain = domain;
                    _logger.LogInformation("Executing script as user: {User}", username);
                }

                var process = new Process { StartInfo = processStartInfo };
                process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogInformation("PSOUT: {Data}", e.Data); };
                process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) _logger.LogError("PSERR: {Data}", e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit(new TimeSpan(0, 30, 0));
                if (!process.HasExited)
                {
                    _logger.LogWarning("Script execution timed out after 30 minutes. Killing the process.");
                    process.Kill();
                    process.WaitForExit(new TimeSpan(0, 1, 0));
                }
                else if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Script execution failed with exit code {process.ExitCode}.");
                }
                else
                {
                    _logger.LogInformation("Script executed successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
            finally
            {
                if (System.IO.File.Exists(tempScriptPath))
                {
                    System.IO.File.Delete(tempScriptPath);
                    _logger.LogInformation("Temporary script file deleted: {Path}", tempScriptPath);
                }
            }
        }

        private static System.Security.SecureString ConvertToSecureString(string str)
        {
            var secure = new System.Security.SecureString();
            foreach (char c in str)
                secure.AppendChar(c);
            secure.MakeReadOnly();
            return secure;
        }
    }
}
