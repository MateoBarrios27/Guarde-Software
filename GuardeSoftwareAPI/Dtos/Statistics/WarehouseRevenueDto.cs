namespace GuardeSoftwareAPI.Dtos.Statistics
{
    public class WarehouseRevenueDto
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int TotalSpaces { get; set; }
        public int OccupiedSpaces { get; set; }
        public decimal OccupancyPercentage { get; set; }
    }
}
