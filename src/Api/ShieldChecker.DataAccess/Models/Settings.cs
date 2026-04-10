using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShieldChecker.DataAccess.Models
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

        public string WorkerVMSize { get; set; }
        public string WorkerVMWindowsImage { get; set; }
        public string WorkerVMLinuxImage { get; set; }
        public string DcVMSize { get; set; }
        public string DcVMImage { get; set; }
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
