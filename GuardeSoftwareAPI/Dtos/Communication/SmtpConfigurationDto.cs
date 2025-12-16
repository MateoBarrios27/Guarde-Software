public class SmtpConfigurationDto
{
    public int? Id { get; set; }
    public string Name { get; set; }  = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public bool EnableBcc { get; set; } 
    public string BccEmail { get; set; } = string.Empty;
}