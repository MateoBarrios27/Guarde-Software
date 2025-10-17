using System;

namespace GuardeSoftwareAPI.Dtos.Payment
{
    public class DetailedPaymentDto
    {
        public int PaymentId { get; set; }                    
        public string ClientName { get; set; } = string.Empty; 
        public string PaymentIdentifier { get; set; } = string.Empty; 
        public string LockerIdentifier { get; set; } = string.Empty;  
        public string Warehouse_name {get; set;} = string.Empty;
        public decimal Amount { get; set; }                    
        public DateTime PaymentDate { get; set; }              
        public string PaymentMethodName { get; set; } = string.Empty; 
        public string Concept { get; set; } = string.Empty;     
        public string MovementType { get; set; } = string.Empty; 
    }
}
