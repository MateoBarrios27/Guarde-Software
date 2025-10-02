using System;

namespace GuardeSoftwareAPI.Dtos.Payment
{
    public class CreatePaymentTransaction{

        //payment
        public int PaymentMethodId { get; set; }
        public int ClientId { get; set; }
        //account movements 
        public int RentalId {get; set;}
        public String MovementType { get; set; } = string.Empty;
        public String? Concept { get; set; } = string.Empty;

        //for payment and acc movements
        public Decimal Amount { get; set; }
    }
}
