namespace GuardeSoftwareAPI.Entities
{
    public class ActivityLog
    {
        public int Id { get; set; }   
        public int UserId { get; set; }         
        public DateTime LogDate { get; set; }    
        public string Action { get; set; } = string.Empty; // e.g., Create, Update, Delete    
        public string TableName { get; set; } = string.Empty; // e.g., User, LockerType, Warehouse, etc.
        public int RecordId { get; set; }      
        public string? OldValue { get; set; } = string.Empty;  
        public string? NewValue { get; set; } = string.Empty; // JSON representation of the old and new values
    }
}