namespace CdrBilling.Domain.Entities;

public sealed class Subscriber
{
    public long Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string PhoneNumber { get; private set; } = default!;
    public string ClientName { get; private set; } = default!;

    private Subscriber() { }

    public static Subscriber Create(Guid sessionId, string phoneNumber, string clientName) => new()
    {
        SessionId = sessionId,
        PhoneNumber = phoneNumber,
        ClientName = clientName
    };
}
