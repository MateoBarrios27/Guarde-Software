namespace GuardeSoftwareAPI.Dtos.AccountMovement
{
    public class CreateAccountMovementDTO
    {
        public int ClientId { get; set; }
        public string MovementType { get; set; } = string.Empty; // "DEBITO" o "CREDITO"
        public decimal Amount { get; set; }
        public string Concept { get; set; } = string.Empty;
        public DateTime? Date { get; set; } = DateTime.UtcNow;
    }
}