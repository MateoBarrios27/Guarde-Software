namespace GuardeSoftwareAPI.Entities
{
    public class BillingType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }
}