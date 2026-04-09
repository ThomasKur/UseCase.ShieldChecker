using ShieldChecker.HostService.Core;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ShieldChecker.HostService
{
    /// <summary>
    /// Interactive CLI setup wizard. Prompts the operator for connection details,
    /// validates permissions against both the ShieldChecker API and the Defender API,
    /// then writes the validated configuration to appsettings.json and optionally
    /// registers the process as a platform service (Windows Service or systemd unit).
    /// </summary>
    public static class SetupWizard
    {
        public static async Task<int> RunAsync()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║       ShieldChecker HostService  –  Setup Wizard     ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            // ─── Step 1 – Gather credentials ───────────────────────────────────
            Console.WriteLine("Step 1 – App Registration credentials");
            Console.WriteLine("──────────────────────────────────────");

            string tenantId = Prompt("Azure AD Tenant ID");
            string clientId = Prompt("App Registration Client ID");
            string clientSecret = PromptSecret("App Registration Client Secret");

            Console.WriteLine();
            Console.WriteLine("Step 2 – ShieldChecker API connection");
            Console.WriteLine("──────────────────────────────────────");

            string apiHostname = Prompt("ShieldChecker API hostname (e.g. api.shieldchecker.example.com)");
            string apiScope = Prompt(
                "ShieldChecker API OAuth scope / Application ID URI\n" +
                "  (e.g. api://00000000-0000-0000-0000-000000000000)");

            // ─── Step 2 – Permission validation ────────────────────────────────
            Console.WriteLine();
            Console.WriteLine("Step 3 – Permission validation");
            Console.WriteLine("──────────────────────────────");
            Console.WriteLine("Acquiring tokens and probing APIs – please wait …");
            Console.WriteLine();

            var tokenService = new TokenService(tenantId, clientId, clientSecret);
            var validator = new PermissionValidator(tokenService);

            bool shieldCheckerOk = await ValidateAndReport(
                "ShieldChecker API",
                () => validator.ValidateShieldCheckerApiAsync(apiHostname, apiScope));

            bool defenderOk = await ValidateAndReport(
                "Microsoft Defender for Endpoint API",
                () => validator.ValidateDefenderApiAsync());

            Console.WriteLine();
            if (!shieldCheckerOk || !defenderOk)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠  One or more permission checks failed.");
                Console.WriteLine("   Please grant the app registration the missing permissions and retry.");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("Continue and save configuration anyway? [y/N] ");
                string? answer = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (answer != "y" && answer != "yes")
                {
                    Console.WriteLine("Setup aborted. No configuration was written.");
                    return 1;
                }
            }

            // ─── Step 3 – Write configuration ──────────────────────────────────
            string appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            var config = new Dictionary<string, object>
            {
                ["Logging"] = new Dictionary<string, object>
                {
                    ["LogLevel"] = new Dictionary<string, string>
                    {
                        ["Default"] = "Information",
                        ["Microsoft.Hosting.Lifetime"] = "Information"
                    }
                },
                ["ShieldCheckerApi"] = new Dictionary<string, string>
                {
                    ["Hostname"] = apiHostname,
                    ["Scope"] = apiScope
                },
                ["AzureAd"] = new Dictionary<string, string>
                {
                    ["TenantId"] = tenantId,
                    ["ClientId"] = clientId,
                    ["ClientSecret"] = clientSecret
                }
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(appSettingsPath, json);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✔  Configuration written to {appSettingsPath}");
            Console.ResetColor();

            // ─── Step 4 – Register as platform service (optional) ──────────────
            Console.WriteLine();
            Console.WriteLine("Step 4 – Register as a system service (optional)");
            Console.WriteLine("─────────────────────────────────────────────────");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.Write("Register as a Windows Service? [y/N] ");
                string? register = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (register == "y" || register == "yes")
                    RegisterWindowsService();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.Write("Install systemd unit file? [y/N] ");
                string? register = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (register == "y" || register == "yes")
                    InstallSystemdUnit();
            }
            else
            {
                Console.WriteLine("Automatic service registration is not supported on this OS.");
                Console.WriteLine("Please register the service manually.");
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✔  Setup complete. Start the service with:");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Console.WriteLine("     sc start ShieldCheckerHostService");
            else
                Console.WriteLine("     sudo systemctl start shieldchecker-hostservice");
            Console.ResetColor();

            return 0;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static async Task<bool> ValidateAndReport(
            string name,
            Func<Task<(bool Success, string? Error)>> validate)
        {
            Console.Write($"  Checking {name} … ");
            try
            {
                var (ok, error) = await validate();
                if (ok)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✔");
                    Console.ResetColor();
                    return true;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✖");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"    → {error}");
                    Console.ResetColor();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✖");
                Console.WriteLine($"    → Unexpected error: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        private static string Prompt(string label)
        {
            Console.Write($"  {label}: ");
            return Console.ReadLine()?.Trim() ?? string.Empty;
        }

        private static string PromptSecret(string label)
        {
            Console.Write($"  {label}: ");
            var secret = new System.Text.StringBuilder();
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Backspace && secret.Length > 0)
                {
                    secret.Remove(secret.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
                {
                    secret.Append(key.KeyChar);
                    Console.Write('*');
                }
            } while (key.Key != ConsoleKey.Enter);
            Console.WriteLine();
            return secret.ToString();
        }

        private static void RegisterWindowsService()
        {
            string exePath = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "ScHostSvc.exe");
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("sc.exe",
                    $"create ShieldCheckerHostService binPath= \"{exePath}\" start= auto DisplayName= \"ShieldChecker Host Service\"")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.WaitForExit(10_000);
                if (proc?.ExitCode == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  ✔ Windows Service registered as 'ShieldCheckerHostService'.");
                    Console.ResetColor();
                }
                else
                {
                    string err = proc?.StandardError.ReadToEnd() ?? string.Empty;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ⚠ sc.exe returned a non-zero exit code. {err}");
                    Console.WriteLine("    You may need to run this wizard as Administrator.");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ⚠ Could not register Windows Service: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void InstallSystemdUnit()
        {
            string exePath = Environment.ProcessPath
                ?? Path.Combine(AppContext.BaseDirectory, "ScHostSvc");
            string unitContent =
                "[Unit]\n" +
                "Description=ShieldChecker Host Service\n" +
                "After=network.target\n\n" +
                "[Service]\n" +
                $"ExecStart={exePath}\n" +
                $"WorkingDirectory={AppContext.BaseDirectory}\n" +
                "Restart=on-failure\n" +
                "RestartSec=10\n" +
                "SyslogIdentifier=shieldchecker-hostservice\n\n" +
                "[Install]\n" +
                "WantedBy=multi-user.target\n";

            string unitPath = "/etc/systemd/system/shieldchecker-hostservice.service";
            try
            {
                File.WriteAllText(unitPath, unitContent);
                System.Diagnostics.Process.Start("systemctl", "daemon-reload")?.WaitForExit(5_000);
                System.Diagnostics.Process.Start("systemctl", "enable shieldchecker-hostservice")?.WaitForExit(5_000);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ✔ systemd unit installed at {unitPath}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  ⚠ Could not install systemd unit: {ex.Message}");
                Console.WriteLine("    You may need to run this wizard with sudo.");
                Console.ResetColor();
            }
        }
    }
}
