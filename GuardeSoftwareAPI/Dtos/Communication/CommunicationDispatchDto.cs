namespace GuardeSoftwareAPI.Dtos.Communication
{
    public class CommunicationDispatchDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
    }
}
