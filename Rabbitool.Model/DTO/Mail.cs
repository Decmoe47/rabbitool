namespace Rabbitool.Model.DTO.Mail;

public class Mail
{
    public required List<AddressInfo> From { get; set; }
    public required List<AddressInfo> To { get; set; }
    public required string Subject { get; set; }
    public required DateTime Time { get; set; }
    public required string Text { get; set; }
}

public class AddressInfo
{
    public required string Address { get; set; }
    public required string Name { get; set; }
}
