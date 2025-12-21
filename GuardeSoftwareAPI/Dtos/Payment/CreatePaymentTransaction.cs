using System;

namespace GuardeSoftwareAPI.Dtos.Payment
{
    public class CreatePaymentTransaction{

        //payment
        public int PaymentMethodId { get; set; }
        public int ClientId { get; set; }
        //account movements 
        // public int RentalId {get; set;} Para mayor sostenibilidad a largo plazo el rentalId correspondiente al cliente se obtiene en el back
        public String MovementType { get; set; } = string.Empty;
        public String? Concept { get; set; } = string.Empty;
        //for payment and acc movements
        public Decimal Amount { get; set; }
        public DateTime Date { get; set; }

        //for logical of advance payments
         public bool IsAdvancePayment { get; set; }
         public int? AdvanceMonths { get; set; }
        
    }
}
