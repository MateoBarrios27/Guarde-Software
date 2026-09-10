namespace GuardeSoftwareAPI.Dtos.Client
{
    public class GetClientsRequestDto
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string SortField { get; set; } = "FullName";
        public string SortDirection { get; set; } = "asc";
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public bool? Active { get; set; } = true;
        public int? WarehouseId { get; set; }
        public List<int>? WarehouseIds { get; set; }
        public List<int>? ExcludedWarehouseIds { get; set; }
        public string? AdvancedFilter { get; set; }
        public List<string>? AdvancedFilters { get; set; }
        public List<string>? ExcludedAdvancedFilters { get; set; }
        public List<string>? IvaConditions { get; set; }
        public List<string>? ExcludedIvaConditions { get; set; }
        public List<int>? BillingTypeIds { get; set; }
        public List<int>? ExcludedBillingTypeIds { get; set; }
        public List<int>? PreferredPaymentMethodIds { get; set; }
        public List<int>? ExcludedPreferredPaymentMethodIds { get; set; }
        public List<int>? LockerTypeIds { get; set; }
        public List<int>? ExcludedLockerTypeIds { get; set; }
        public List<int>? PaymentDays { get; set; }
        public List<int>? ExcludedPaymentDays { get; set; }
    }
}
