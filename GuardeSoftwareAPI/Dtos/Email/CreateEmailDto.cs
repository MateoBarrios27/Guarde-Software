using System.ComponentModel.DataAnnotations;

namespace GuardeSoftwareAPI.Dtos.Email
{
    public class CreateEmailDto
    {
        public int ClientId { get; set; }
        [EmailAddress(ErrorMessage = "La dirección de correo electrónico no es válida.")]
        public string Address { get; set; } = string.Empty;
        public string? Type { get; set; } = string.Empty;
    }
}