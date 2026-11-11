public class FinancialAccountDto {
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? Color { get; set; }
    // Nota de la cuenta (como el comentario de un movimiento): vive en la fila del mes y se copia al mes siguiente.
    public string? Comment { get; set; }
    public DateTime? CommentUpdatedAt { get; set; }
}
