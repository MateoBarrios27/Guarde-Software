namespace GuardeSoftwareAPI.Dtos.Client
{
    public class GetClientsRequestDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortField { get; set; } = "FirstName";
        public string SortDirection { get; set; } = "asc";
        public string? SearchTerm { get; set; }
        public bool? Active { get; set; } = true;
    }
}