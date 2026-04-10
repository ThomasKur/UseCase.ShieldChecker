namespace ShieldChecker.DataAccess.Models
{
    public class SharedTestConsumption
    {
        public int ID { get; set; }

        public int SharedTestDefinitionId { get; set; }

        public SharedTestDefinition SharedTestDefinition { get; set; } = default!;

        public Guid ConsumedByUserId { get; set; }

        public UserInfo ConsumedBy { get; set; } = default!;

        public DateTime ConsumedAt { get; set; }
    }
}
