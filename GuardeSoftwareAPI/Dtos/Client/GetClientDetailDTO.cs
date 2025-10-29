using GuardeSoftwareAPI.Dtos.Locker;

namespace GuardeSoftwareAPI.Dtos.Client
{
    public class GetClientDetailDTO
    {
        //Personal Information
        public int Id { get; set; }
        public decimal PaymentIdentifier { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string Cuit { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; }

        //Contact Information
        public string[] Email { get; set; } = [];
        public string[] Phone { get; set; } = [];
        public string Address { get; set; } = string.Empty;

        //Payment & rental Information
        public string IvaCondition { get; set; } = string.Empty;
        public string PreferredPaymentMethod { get; set; } = string.Empty;
        public decimal IncreasePercentage { get; set; }
        public int IncreaseFrequency { get; set; }
        public DateTime NextIncreaseDay { get; set; }
        public DateTime NextPaymentDay { get; set; }
        public decimal Balance { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal RentAmount { get; set; }

        //Locker Information
        //If the client no longer rents lockers, this list will be empty
        public List<GetLockerClientDetailDTO>? LockersList { get; set; }
        public decimal ContractedM3 { get; set; }

        //Other Information
        public string Notes { get; set; } = string.Empty;

        //Maybe add a list of past payments?

    }
}