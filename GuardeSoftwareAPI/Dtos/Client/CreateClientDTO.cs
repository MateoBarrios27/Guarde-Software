using System;
using GuardeSoftwareAPI.Dtos.Address;

namespace GuardeSoftwareAPI.Dtos.Client
{

    public class CreateClientDTO
    {
        //Client table
        public int? Id { get; set; }
        public decimal? PaymentIdentifier { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; }
        public string? Notes { get; set; } = string.Empty;
        public string? Dni { get; set; } = string.Empty;
        public string? Cuit { get; set; } = string.Empty;
        public int? PreferredPaymentMethodId { get; set; }
        public string? IvaCondition { get; set; } = string.Empty;

        //Rental Table
        public DateTime StartDate { get; set; }
        // Maybe add End date??
        public decimal? ContractedM3 { get; set; }

        //rentalAmountHistory
        public decimal Amount { get; set; }
        //id of locker rental
        public List<int> LockerIds { get; set; } = [];
        //user id for activityLog
        public int UserID { get; set; }

        //Contact Info
        public List<string> Emails { get; set; } = [];
        public List<string> Phones { get; set; } = [];
        public CreateAddressDto AddressDto { get; set; } = new CreateAddressDto();
    }
}