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
        /// <summary>
        /// Clientes actualmente asignados, sin importar si la relación vive en
        /// lockers.rental_id o en rental_lockers.
        /// </summary>
        public List<LockerClientSummary> Clients { get; set; } = [];
        public bool IsFreeSpace { get; set; } = false;
    }

    public class LockerClientSummary
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public int PaymentIdentifier { get; set; }
    }
}
