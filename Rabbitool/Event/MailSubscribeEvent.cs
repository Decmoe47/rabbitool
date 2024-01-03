namespace Rabbitool.Event;

public static class MailSubscribeEvent
{
    public delegate void AddMailSubscribeDelegate(
        string host, int port, bool usingSsl, string address, string password, string mailbox = "INBOX");

    public delegate Task DeleteMailSubscribeDelegate(string address, CancellationToken ct = default);

    public static event AddMailSubscribeDelegate? AddMailSubscribeEvent;

    public static event DeleteMailSubscribeDelegate? DeleteMailSubscribeEvent;

    public static void OnMailSubscribeAdded(
        string host, int port, bool usingSsl, string address, string password, string mailbox = "INBOX")
    {
        AddMailSubscribeEvent?.Invoke(host, port, usingSsl, address, password, mailbox);
    }

    public static async Task OnMailSubscribeDeletedAsync(string address, CancellationToken ct = default)
    {
        if (DeleteMailSubscribeEvent != null)
            await DeleteMailSubscribeEvent(address, ct);
    }
}