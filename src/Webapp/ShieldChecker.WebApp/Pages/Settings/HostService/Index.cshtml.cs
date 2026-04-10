using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ShieldChecker.WebApp.Pages.Settings.HostService
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// The Application ID URI (scope) exposed by the ShieldChecker API app registration.
        /// Populated from the AzureAd:ClientId configuration value so the generated script is
        /// pre-filled with the correct audience.
        /// </summary>
        public string ShieldCheckerApiAppId { get; private set; } = string.Empty;

        /// <summary>
        /// Pre-filled PowerShell script that creates the HostService app registration and
        /// assigns all required API permissions in the customer's tenant.
        /// </summary>
        public string AppRegistrationScript { get; private set; } = string.Empty;

        public IndexModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            ShieldCheckerApiAppId = _configuration["AzureAd:ClientId"] ?? "<ShieldChecker-API-Client-ID>";
            AppRegistrationScript = BuildAppRegistrationScript(ShieldCheckerApiAppId);
        }

        private static string BuildAppRegistrationScript(string shieldCheckerApiClientId)
        {
            return $@"#Requires -Modules Microsoft.Graph.Applications, Microsoft.Graph.Authentication
<#
.SYNOPSIS
    Creates an App Registration for the ShieldChecker HostService and grants all
    required API permissions, then outputs the credentials to use in the setup wizard.

.DESCRIPTION
    Run this script as a Global Administrator or Application Administrator in the
    target Azure AD tenant.  Admin consent is granted automatically for all
    assigned application permissions.

.NOTES
    Required module: Microsoft.Graph (Install-Module Microsoft.Graph -Scope CurrentUser)
#>

[CmdletBinding()]
param(
    [string]$AppName = 'ShieldChecker-HostService'
)

Connect-MgGraph -Scopes 'Application.ReadWrite.All','AppRoleAssignment.ReadWrite.All'

# ── Create the App Registration ──────────────────────────────────────────────
$app = New-MgApplication -DisplayName $AppName
Write-Host ""App registration created: $($app.DisplayName)  (AppId: $($app.AppId))""

# Create the service principal so role assignments can be made
$sp = New-MgServicePrincipal -AppId $app.AppId
Write-Host ""Service principal created: $($sp.Id)""

# ── Create a client secret ────────────────────────────────────────────────────
$secretParams = @{{
    PasswordCredential = @{{
        DisplayName = 'HostService-Secret'
        EndDateTime = (Get-Date).AddYears(1)
    }}
}}
$secret = Add-MgApplicationPassword -ApplicationId $app.Id -BodyParameter $secretParams
Write-Host ""Client secret created (valid for 1 year).""

# ── Assign permissions to the ShieldChecker API ───────────────────────────────
$shieldCheckerApiAppId = '{shieldCheckerApiClientId}'
$shieldCheckerSp = Get-MgServicePrincipal -Filter ""appId eq '$shieldCheckerApiAppId'""

if ($shieldCheckerSp) {{
    foreach ($role in $shieldCheckerSp.AppRoles) {{
        New-MgServicePrincipalAppRoleAssignment `
            -ServicePrincipalId $sp.Id `
            -AppRoleId $role.Id `
            -PrincipalId $sp.Id `
            -ResourceId $shieldCheckerSp.Id | Out-Null
        Write-Host ""  Granted ShieldChecker permission: $($role.Value)""
    }}
}} else {{
    Write-Warning ""ShieldChecker API service principal not found (appId: $shieldCheckerApiAppId). Skipping ShieldChecker permissions.""
}}

# ── Assign permissions to the Defender for Endpoint API ──────────────────────
$mdeAppId     = 'fc780465-2017-40d4-a0c5-307022471b92'
$mdePermissions = @(
    'Machine.ReadWrite.All',
    'AdvancedQuery.Read.All',
    'Alert.ReadWrite.All',
    'SecurityRecommendation.Read.All'
)
$mdeSp = Get-MgServicePrincipal -Filter ""appId eq '$mdeAppId'""

foreach ($perm in $mdePermissions) {{
    $role = $mdeSp.AppRoles | Where-Object Value -EQ $perm | Select-Object -First 1
    if ($role) {{
        New-MgServicePrincipalAppRoleAssignment `
            -ServicePrincipalId $sp.Id `
            -AppRoleId $role.Id `
            -PrincipalId $sp.Id `
            -ResourceId $mdeSp.Id | Out-Null
        Write-Host ""  Granted Defender permission: $perm""
    }} else {{
        Write-Warning ""  Permission '$perm' not found on Defender API – skipping.""
    }}
}}

# ── Output credentials ────────────────────────────────────────────────────────
$tenantId = (Get-MgContext).TenantId

Write-Host """"
Write-Host ""════════════════════════════════════════════════════════""
Write-Host ""  Copy these values into the ShieldChecker HostService""
Write-Host ""  setup wizard  (ScHostSvc --setup).""
Write-Host ""════════════════════════════════════════════════════════""
Write-Host ""  Tenant ID    : $tenantId""
Write-Host ""  Client ID    : $($app.AppId)""
Write-Host ""  Client Secret: $($secret.SecretText)""
Write-Host ""  API Scope    : api://$shieldCheckerApiAppId""
Write-Host ""════════════════════════════════════════════════════════""
Write-Host """"
Write-Warning ""Store the client secret securely – it will not be shown again.""
";
        }
    }
}
