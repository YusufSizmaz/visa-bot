namespace VisaTelegramBot.Domain.ChannelMessages;

public enum ChannelMessageStatus
{
    /// <summary>Gonderim zamanini bekliyor (hemen gonderilecek mesajlar da bu durumla baslar).</summary>
    Scheduled = 1,

    Sent = 2,

    Failed = 3,

    Cancelled = 4
}

public enum ChannelMessageKind
{
    /// <summary>Yoneticinin panelden yazdigi mesaj.</summary>
    Custom = 1,

    /// <summary>Ucus firsatindan uretilen mesaj.</summary>
    FlightDeal = 2
}
