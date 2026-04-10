using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShieldChecker.HostService.Core
{
    /// <summary>
    /// Acquires OAuth 2.0 access tokens using the client credentials flow.
    /// </summary>
    public class TokenService
    {
        private readonly HttpClient _httpClient;
        private readonly string _tenantId;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private record TokenResponse(
            [property: JsonPropertyName("access_token")] string AccessToken,
            [property: JsonPropertyName("expires_in")] int ExpiresIn,
            [property: JsonPropertyName("token_type")] string TokenType
        );

        public TokenService(string tenantId, string clientId, string clientSecret, HttpClient? httpClient = null)
        {
            _tenantId = tenantId;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _httpClient = httpClient ?? new HttpClient();
        }

        /// <summary>
        /// Acquires an access token for the given resource using the client credentials flow.
        /// </summary>
        /// <param name="resource">The resource URI (audience), e.g. "https://api.securitycenter.microsoft.com".</param>
        public async Task<string> AcquireTokenAsync(string resource, CancellationToken cancellationToken = default)
        {
            string tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";

            var formData = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["scope"] = $"{resource}/.default"
            };

            var response = await _httpClient.PostAsync(
                tokenEndpoint,
                new FormUrlEncodedContent(formData),
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json)
                ?? throw new InvalidOperationException("Failed to deserialize token response.");

            return tokenResponse.AccessToken;
        }
    }
}
