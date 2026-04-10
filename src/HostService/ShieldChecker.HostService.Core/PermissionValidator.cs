using System.Net;
using System.Net.Http.Headers;

namespace ShieldChecker.HostService.Core
{
    /// <summary>
    /// Validates that the configured app registration has the required permissions
    /// to call the ShieldChecker API and the Microsoft Defender for Endpoint API.
    /// </summary>
    public class PermissionValidator
    {
        private readonly TokenService _tokenService;

        private const string DefenderApiResource = "https://api.securitycenter.microsoft.com";
        private const string DefenderApiProbeUrl = "https://api.securitycenter.microsoft.com/api/machines?$top=1";

        public PermissionValidator(TokenService tokenService)
        {
            _tokenService = tokenService;
        }

        /// <summary>
        /// Validates access to the ShieldChecker API by requesting a token for the given scope
        /// and performing a GET /api/Job request against the API hostname.
        /// Returns (true, null) on success or (false, errorMessage) on failure.
        /// </summary>
        public async Task<(bool Success, string? Error)> ValidateShieldCheckerApiAsync(
            string shieldCheckerApiHostname,
            string shieldCheckerApiScope,
            CancellationToken ct = default)
        {
            try
            {
                string token = await _tokenService.AcquireTokenAsync(shieldCheckerApiScope, ct);
                using var httpClient = new HttpClient();
                string url = $"https://{shieldCheckerApiHostname}/api/Job?workername=wizard-probe";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return (false, "HTTP 401 Unauthorized – the app registration is missing the required API permissions for ShieldChecker.");

                // 404 / 400 are acceptable (no job queued) – what matters is that auth succeeded
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Error contacting ShieldChecker API: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates access to the Microsoft Defender for Endpoint API.
        /// Returns (true, null) on success or (false, errorMessage) on failure.
        /// </summary>
        public async Task<(bool Success, string? Error)> ValidateDefenderApiAsync(CancellationToken ct = default)
        {
            try
            {
                string token = await _tokenService.AcquireTokenAsync(DefenderApiResource, ct);
                using var httpClient = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, DefenderApiProbeUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return (false, "HTTP 401 Unauthorized – the app registration is missing the required permissions for the Microsoft Defender for Endpoint API.");

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Error contacting Defender API: {ex.Message}");
            }
        }
    }
}
