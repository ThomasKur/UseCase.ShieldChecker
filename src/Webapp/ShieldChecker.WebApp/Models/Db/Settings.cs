using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShieldChecker.WebApp.Models.Db
{

    public class Settings
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        [Required]
        [Display(Name = "Max Worker (VM) Count")]
        public int MaxWorkerCount { get; set; }
        [Display(Name = "Job Timeout (Minutes)")]
        public int JobTimeout { get; set; }
        [Display(Name = "Job Review Enabled")]
        public bool JobReview { get; set; }

        [Display(Name = "Worker VM CPU Count")]
        [Range(1, 128, ErrorMessage = "CPU count must be between 1 and 128.")]
        public int WorkerVMCpuCount { get; set; }
        [Display(Name = "Worker VM Memory (MB)")]
        [Range(512, 1048576, ErrorMessage = "Memory must be between 512 MB and 1048576 MB.")]
        public long WorkerVMMemoryMB { get; set; }
        public string WorkerVMWindowsImage { get; set; }
        public string WorkerVMLinuxImage { get; set; }
        [Display(Name = "DC VM CPU Count")]
        [Range(1, 128, ErrorMessage = "CPU count must be between 1 and 128.")]
        public int DcVMCpuCount { get; set; }
        [Display(Name = "DC VM Memory (MB)")]
        [Range(512, 1048576, ErrorMessage = "Memory must be between 512 MB and 1048576 MB.")]
        public long DcVMMemoryMB { get; set; }
        public string DcVMImage { get; set; }
        [Display(Name = "VM Storage Path")]
        [StringLength(500, ErrorMessage = "Must be max 500 characters long.")]
        public string VMStoragePath { get; set; }
        [Display(Name = "Domain FQDN")]
        [RegularExpression(@"(?=^.{4,253}$)(^((?!-)[a-zA-Z0-9-]{1,63}(?<!-)\.)+[a-zA-Z]{2,63}$)", ErrorMessage = "Please enter a valid DNS FQDN.")]
        [StringLength(253, ErrorMessage = "Must be max 253 characters long.")]
        public string DomainFQDN { get; set; }
        [Display(Name = "Domain Controller Name (Netbios)")]
        [RegularExpression(@"^[a-z0-9]*$", ErrorMessage = "Only letters and numbers are allowed.")]
        [StringLength(15, ErrorMessage = "Must be at least 3 and at max 15 characters long.", MinimumLength = 3)]
        public string DomainControllerName { get; set; }

        

        public string MDEWindowsOnboardingScript { get; set; }
        public string MDELinuxOnboardingScript { get; set; }

        public bool IsInitialized()
        {
            if (
                String.IsNullOrWhiteSpace(DomainFQDN) 
                || String.IsNullOrWhiteSpace(MDELinuxOnboardingScript) 
                || String.IsNullOrWhiteSpace(MDELinuxOnboardingScript) 
                )
            {
                return false;
            }
            else
            {
                return true;
            }
        }

    }
}
