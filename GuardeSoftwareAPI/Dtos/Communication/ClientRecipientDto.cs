namespace GuardeSoftwareAPI.Dtos.Communication
{
    public class ClientRecipientDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal Balance { get; set; } 
        public int MaxUnpaidMonths { get; set; }
    }
}