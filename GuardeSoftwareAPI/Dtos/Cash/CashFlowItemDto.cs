public class CashFlowItemDto {
    public int Id { get; set; }
    public DateTime? Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Comment { get; set; } = string.Empty;
    public decimal Depo { get; set; }
    public decimal Casa { get; set; }
    public bool IsPaid { get; set; }
    public decimal Retiros { get; set; }
    public decimal Extras { get; set; }
    public int DisplayOrder { get; set; }
}