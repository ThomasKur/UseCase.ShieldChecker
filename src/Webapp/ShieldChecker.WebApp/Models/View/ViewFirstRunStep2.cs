namespace ShieldChecker.WebApp.Models.View
{
    public class ViewFirstRunStep2
    {
        public List<string> MsGraphScopes { get; set; } = new List<string>();
        public List<string> RequiredMsGraphScopes { get; set; } = new List<string>();
        public bool IsMsGraphOK { get; set; }

        public List<string> MsMdeScopes { get; set; } = new List<string>();
        public List<string> RequiredMsMdeScopes { get; set; } = new List<string>();
        public bool IsMsMdeOK { get; set; }

        public string RemediationScript { get; set; } = string.Empty;

        /// <summary>
        /// Pre-filled PowerShell script for creating the HostService app registration.
        /// Shown as an informational section in the First-Run Wizard Step 2.
        /// </summary>
        public string HostServiceAppRegistrationScript { get; set; } = string.Empty;
    }
}
