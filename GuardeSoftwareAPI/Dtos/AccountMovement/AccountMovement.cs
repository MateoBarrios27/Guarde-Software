namespace GuardeSoftwareAPI.Dtos.AccountMovement
{
    // Este DTO lo usará el frontend para enviar un nuevo movimiento
    public class CreateAccountMovementDTO
    {
        public int ClientId { get; set; }
        public string MovementType { get; set; } = string.Empty; // "DEBITO" o "CREDITO"
        public decimal Amount { get; set; }
        public string Concept { get; set; } = string.Empty;
    }
}