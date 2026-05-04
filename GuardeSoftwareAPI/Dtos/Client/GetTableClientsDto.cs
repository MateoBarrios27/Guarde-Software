namespace GuardeSoftwareAPI.Dtos.Client
{
    public class GetTableClientsDto
    {
        public int Id { get; set; }
        public decimal? PaymentIdentifier { get; set; }
        public string FullName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal PreviousBalance { get; set; }
        public decimal InterestAmount { get; set; }    
        public decimal CurrentRent { get; set; }
        public string Status { get; set; } = string.Empty; // 'Al Día', 'Pendiente', 'Moroso', 'Baja'
        public List<string>? Lockers { get; set; } = null;
        public List<WarehouseLockerItem>? WarehouseLockers { get; set; } = null;

        public bool Active { get; set; }

    }

    public class WarehouseLockerItem
    {
        public string Warehouse { get; set; } = string.Empty;
        public string Lockers { get; set; } = string.Empty;
    }
}