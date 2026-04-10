using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ShieldChecker.DataAccess.Models
{
    
    public class SystemStatus
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ID { get; set; }
        public bool IsFirstRunCompleted { get; set; }
        public DomainControllerStatus DomainControllerStatus { get; set; }
        public string DomainControllerLog { get; set; }
        public string WebAppVersion { get; set; }
    }
}
