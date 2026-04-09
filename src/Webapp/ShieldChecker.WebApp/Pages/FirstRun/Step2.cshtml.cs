using Azure.Core;
using Azure.Identity;
using ShieldChecker.WebApp.Models.View;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using System.IdentityModel.Tokens.Jwt;

namespace ShieldChecker.WebApp.Pages.FirstRun
{
    public class Step2 : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ShieldCheckerContext _context;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IConfiguration _configuration;
        private readonly List<string> requiredGraphScopes;
        private readonly List<string> requiredMsMdeScopes;

        public Step2(ILogger<IndexModel> logger, ShieldCheckerContext context, IHostEnvironment hostEnvironment, IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _hostEnvironment = hostEnvironment;
            _configuration = configuration;
            requiredGraphScopes = new List<string> { "SecurityAlert.ReadWrite.All" };
            requiredMsMdeScopes = new List<string> { "Machine.ReadWrite.All",  "Machine.Offboard" };
        }
        [BindProperty]
        public ViewFirstRunStep2 FirstRun { get; set; } = default!;



        public async Task<IActionResult> OnGetAsync()
        {
            FirstRun = new ViewFirstRunStep2();
            
            if (!_hostEnvironment.IsDevelopment())
            {
                
                try
                {
                    var d = new DefaultAzureCredential();
                    var graphScopes = GetScopesFromCredentials(d, "https://graph.microsoft.com");
                    FirstRun.MsGraphScopes = graphScopes;
                    FirstRun.IsMsGraphOK = graphScopes.Intersect(requiredGraphScopes).Count() == requiredGraphScopes.Count;
                    // Debug
                    var t =d.GetToken(new Azure.Core.TokenRequestContext(new string[] { "https://graph.microsoft.com" }));
                    var handler = new JwtSecurityTokenHandler();
                    var TokenDecoded = handler.ReadJwtToken(t.Token);
                    FirstRun.MsGraphScopes.Add("Payload: " + TokenDecoded.RawPayload);
                    FirstRun.MsGraphScopes.Add("Header: " + TokenDecoded.RawHeader);
                    FirstRun.MsGraphScopes.Add("Token: " + t.Token);

                } catch (Exception e)
                {
                    FirstRun.MsGraphScopes = [e.Message];
                    if(e.StackTrace != null)
                        FirstRun.MsGraphScopes.Add(e.StackTrace);
                    FirstRun.IsMsGraphOK = false;
                }
                FirstRun.RequiredMsGraphScopes = requiredGraphScopes;
                try
                {
                    var d = new DefaultAzureCredential();
                    var mdeScopes = GetScopesFromCredentials(d, "https://api.securitycenter.microsoft.com");
                    FirstRun.MsMdeScopes = mdeScopes;
                    FirstRun.IsMsMdeOK = mdeScopes.Intersect(requiredMsMdeScopes).Count() == requiredMsMdeScopes.Count;
                    FirstRun.RemediationScript = "# Manual assign Azure Custom RBAC Role to App Service Principle\r\n$managedIdentityId = '" + GetOid(d, "https://graph.microsoft.com") + "'\r\n$myPermissions = \"Machine.Offboard\", \"Machine.ReadWrite.All\"\r\n$myGPermissions = \"SecurityAlert.ReadWrite.All\"\r\n\r\nConnect-MgGraph -Scopes 'Application.ReadWrite.All,AppRoleAssignment.ReadWrite.All'\r\n\r\n$msi = Get-MgServicePrincipal -Filter \"Id eq '$managedIdentityId'\"\r\n\r\n$mde = Get-MgServicePrincipal -Filter \"AppId eq 'fc780465-2017-40d4-a0c5-307022471b92'\"\r\n\r\nforeach ($myPerm in $myPermissions) {\r\n  $permission = $mde.AppRoles `\r\n      | Where-Object Value -Like $myPerm `\r\n      | Select-Object -First 1\r\n\r\n  if ($permission) {\r\n    New-MgServicePrincipalAppRoleAssignment `\r\n        -ServicePrincipalId $msi.Id `\r\n        -AppRoleId $permission.Id `\r\n        -PrincipalId $msi.Id `\r\n        -ResourceId $mde.Id\r\n  }\r\n}\r\n\r\n$graph = Get-MgServicePrincipal -Filter \"AppId eq '00000003-0000-0000-c000-000000000000'\"\r\n\r\nforeach ($myPerm in $myGPermissions) {\r\n  $permission = $graph.AppRoles `\r\n      | Where-Object Value -Like $myPerm `\r\n      | Select-Object -First 1\r\n\r\n  if ($permission) {\r\n    New-MgServicePrincipalAppRoleAssignment `\r\n        -ServicePrincipalId $msi.Id `\r\n        -AppRoleId $permission.Id `\r\n        -PrincipalId $msi.Id `\r\n        -ResourceId $graph.Id\r\n  }\r\n}";

                }
                catch (Exception e)
                {
                    FirstRun.MsMdeScopes = [$"Error: {e.Message}"];
                    if (e.StackTrace != null)
                        FirstRun.MsMdeScopes.Add(e.StackTrace);
                    FirstRun.IsMsMdeOK = false;
                }
                FirstRun.RequiredMsMdeScopes = requiredMsMdeScopes;
                
            }
            else
            {
                FirstRun.MsGraphScopes = new List<string>();
                FirstRun.RequiredMsGraphScopes = requiredGraphScopes;
                FirstRun.IsMsGraphOK = true;
                FirstRun.MsMdeScopes = new List<string>();
                FirstRun.RequiredMsMdeScopes = requiredMsMdeScopes;
                FirstRun.IsMsMdeOK = true;
                FirstRun.RemediationScript = "# Manual assign Azure Custom RBAC Role to App Service Principle\r\n$managedIdentityId = 'YOURAPPID'\r\n$myPermissions = \"Machine.Offboard\", \"Machine.ReadWrite.All\"\r\n$myGPermissions = \"SecurityAlert.ReadWrite.All\"\r\n\r\nConnect-MgGraph -Scopes 'Application.ReadWrite.All,AppRoleAssignment.ReadWrite.All'\r\n\r\n$msi = Get-MgServicePrincipal -Filter \"Id eq '$managedIdentityId'\"\r\n\r\n$mde = Get-MgServicePrincipal -Filter \"AppId eq 'fc780465-2017-40d4-a0c5-307022471b92'\"\r\n\r\nforeach ($myPerm in $myPermissions) {\r\n  $permission = $mde.AppRoles `\r\n      | Where-Object Value -Like $myPerm `\r\n      | Select-Object -First 1\r\n\r\n  if ($permission) {\r\n    New-MgServicePrincipalAppRoleAssignment `\r\n        -ServicePrincipalId $msi.Id `\r\n        -AppRoleId $permission.Id `\r\n        -PrincipalId $msi.Id `\r\n        -ResourceId $mde.Id\r\n  }\r\n}\r\n\r\n$graph = Get-MgServicePrincipal -Filter \"AppId eq '00000003-0000-0000-c000-000000000000'\"\r\n\r\nforeach ($myPerm in $myGPermissions) {\r\n  $permission = $graph.AppRoles `\r\n      | Where-Object Value -Like $myPerm `\r\n      | Select-Object -First 1\r\n\r\n  if ($permission) {\r\n    New-MgServicePrincipalAppRoleAssignment `\r\n        -ServicePrincipalId $msi.Id `\r\n        -AppRoleId $permission.Id `\r\n        -PrincipalId $msi.Id `\r\n        -ResourceId $graph.Id\r\n  }\r\n}";

            }


            FirstRun.HostServiceAppRegistrationScript = BuildHostServiceAppRegistrationScript(
                _configuration["AzureAd:ClientId"] ?? "<ShieldChecker-API-Client-ID>");

            return Page();
        }

        private List<string> GetScopesFromCredentials(DefaultAzureCredential Credential, string RessourceUrl)
        {
            var t = Credential.GetToken(new Azure.Core.TokenRequestContext(new string[] { RessourceUrl }));
            var handler = new JwtSecurityTokenHandler();
            var TokenDecoded = handler.ReadJwtToken(t.Token);
            
            return TokenDecoded.Claims.Where(c => c.Type == "roles").Select(c => c.Value).ToList();
        }

        private string GetOid(DefaultAzureCredential Credential, string RessourceUrl)
        {
            var t = Credential.GetToken(new Azure.Core.TokenRequestContext(new string[] { RessourceUrl }));
            var handler = new JwtSecurityTokenHandler();
            var TokenDecoded = handler.ReadJwtToken(t.Token);
            return TokenDecoded.Claims.Where(c => c.Type == "oid").First().Value;
        }

        private static string BuildHostServiceAppRegistrationScript(string shieldCheckerApiClientId)
        {
            return $@"#Requires -Modules Microsoft.Graph.Applications, Microsoft.Graph.Authentication
<#
.SYNOPSIS
    Creates the ShieldChecker-HostService App Registration and grants all required
    API permissions, then outputs the credentials for use in the setup wizard.

.NOTES
    Run as Global Administrator or Application Administrator.
    Prerequisites: Install-Module Microsoft.Graph -Scope CurrentUser
#>

[CmdletBinding()]
param([string]$AppName = 'ShieldChecker-HostService')

Connect-MgGraph -Scopes 'Application.ReadWrite.All','AppRoleAssignment.ReadWrite.All'

$app = New-MgApplication -DisplayName $AppName
$sp  = New-MgServicePrincipal -AppId $app.AppId
$secret = Add-MgApplicationPassword -ApplicationId $app.Id `
    -BodyParameter @{{ PasswordCredential = @{{ DisplayName='HostService-Secret'; EndDateTime=(Get-Date).AddYears(1) }} }}

# ShieldChecker API permissions
$shieldCheckerSp = Get-MgServicePrincipal -Filter ""appId eq '{shieldCheckerApiClientId}'""
if ($shieldCheckerSp) {{
    foreach ($role in $shieldCheckerSp.AppRoles) {{
        New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $sp.Id `
            -AppRoleId $role.Id -PrincipalId $sp.Id -ResourceId $shieldCheckerSp.Id | Out-Null
    }}
}}

# Defender for Endpoint API permissions
$mdeSp = Get-MgServicePrincipal -Filter ""appId eq 'fc780465-2017-40d4-a0c5-307022471b92'""
foreach ($perm in @('Machine.ReadWrite.All','AdvancedQuery.Read.All','Alert.ReadWrite.All','SecurityRecommendation.Read.All')) {{
    $role = $mdeSp.AppRoles | Where-Object Value -EQ $perm | Select-Object -First 1
    if ($role) {{ New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $sp.Id `
        -AppRoleId $role.Id -PrincipalId $sp.Id -ResourceId $mdeSp.Id | Out-Null }}
}}

$tenantId = (Get-MgContext).TenantId
Write-Host ""Tenant ID    : $tenantId""
Write-Host ""Client ID    : $($app.AppId)""
Write-Host ""Client Secret: $($secret.SecretText)""
Write-Host ""API Scope    : api://{shieldCheckerApiClientId}""
Write-Warning ""Store the client secret securely – it will not be shown again.""";
        }

    }
}
