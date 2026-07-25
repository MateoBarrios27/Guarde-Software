namespace GuardeSoftwareAPI.Entities
{
    public class Locker
    {
        public int Id { get; set; }
        public int WarehouseId { get; set; } 
        public int LockerTypeId { get; set; } 
        public int? RentalId { get; set; }
        public string? Identifier { get; set; } = string.Empty;
        public string? Features { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ClientName { get; set; }
        /// <summary>
        /// Para espacios libres, lista concatenada de clientes asignados (via rental_lockers).
        /// </summary>
        public string? ClientNames { get; set; }
        public bool IsFreeSpace { get; set; } = false;
    }
}