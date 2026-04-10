using System.Security.Claims;

namespace ShieldChecker.WebApp.Helper
{
    public static class AuthorizationHelper
    {
        /// <summary>
        /// Returns true when the signed-in user belongs to the managing (home) tenant
        /// configured in AzureAd:TenantId.  This gates admin-only features such as
        /// approving/rejecting shared test submissions and viewing draft entries.
        /// </summary>
        public static bool IsManagingTenantUser(ClaimsPrincipal user, IConfiguration config)
        {
            var configuredTenantId = config["AzureAd:TenantId"];
            if (string.IsNullOrWhiteSpace(configuredTenantId))
                return false;

            var userTenantId = user.Claims
                .FirstOrDefault(c => c.Type == "http://schemas.microsoft.com/identity/claims/tenantid"
                                  || c.Type == "tid")
                ?.Value;

            return string.Equals(configuredTenantId, userTenantId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
