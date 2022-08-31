namespace Rabbitool.Model.DTO.Mail;

public class Mail
{
    public List<AddressInfo> From { get; set; } = new List<AddressInfo>();
    public List<AddressInfo> To { get; set; } = new List<AddressInfo>();
    public string Subject { get; set; } = string.Empty;
    public DateTime Time { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class AddressInfo
{
    public string Address { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
