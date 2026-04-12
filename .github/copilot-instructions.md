# Copilot Instructions for ShieldChecker

## Package Policy — Microsoft-published packages only

**Only Microsoft-published NuGet packages are permitted in production projects.**
No third-party runtime libraries may be added.

### Rationale
ShieldChecker is a security-testing platform. Minimising the external supply chain reduces vulnerability exposure and simplifies audit and compliance.

### Permitted package namespaces / publishers

All packages whose NuGet publisher is `dotnet`, `Microsoft`, or `azure-sdk` are permitted.
This includes (but is not limited to):

| Prefix | Examples |
|---|---|
| `Microsoft.*` | `Microsoft.EntityFrameworkCore.*`, `Microsoft.Identity.Web`, `Microsoft.ApplicationInsights.*`, `Microsoft.AspNetCore.*`, `Microsoft.Extensions.*`, `Microsoft.IdentityModel.*` |
| `Azure.*` | `Azure.Identity`, `Azure.Security.KeyVault.*`, `Azure.Extensions.AspNetCore.*` |
| `System.*` | `System.IdentityModel.Tokens.Jwt` |
| `Asp.Versioning.*` | Published by the .NET team under the `dotnet` GitHub org |

### Explicitly prohibited in production projects

- Any third-party structured-logging library (e.g. **Serilog**, NLog, log4net)  
  → Use `Microsoft.Extensions.Logging` (built-in) and `Microsoft.ApplicationInsights.AspNetCore`
- Any third-party health-check helper (e.g. **AspNetCore.HealthChecks.SqlServer**)  
  → Use the built-in `IHealthCheck` interface with EF Core's `CanConnectAsync`
- Any third-party Blazor / UI component library (e.g. **PSC.Blazor.Components.Chartjs**, MudBlazor)  
  → Use `IJSRuntime` interop with a CDN-loaded JavaScript library for client-side rendering
- Any third-party HTTP client helpers (e.g. RestSharp, Flurl)  
  → Use `System.Net.Http.HttpClient` with `Microsoft.Extensions.Http.Resilience`
- Any third-party serialisation library (e.g. Newtonsoft.Json)  
  → Use `System.Text.Json`
- Any third-party mapping library (e.g. AutoMapper)  
  → Write explicit mapping methods

### Test projects are exempt

Test-only packages such as `xunit`, `coverlet.collector`, and `Moq` are permitted in
`*.Tests` projects because they are **not deployed** and do not affect the production
supply chain. They must only appear in projects whose name ends with `.Tests`.

### Enforcing the policy

When suggesting a NuGet package reference, always verify:
1. The package's NuGet publisher is `dotnet`, `Microsoft`, or `azure-sdk`.
2. The package is not flagged as deprecated or has known high/critical CVEs
   in the GitHub Advisory Database.
3. A Microsoft-native alternative does not already exist in the framework.

If you cannot satisfy a requirement with a Microsoft-published package, raise the
limitation in a comment on the issue or PR instead of adding a third-party dependency.
