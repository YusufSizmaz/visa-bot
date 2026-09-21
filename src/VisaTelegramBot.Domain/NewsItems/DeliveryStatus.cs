namespace VisaTelegramBot.Domain.NewsItems;

public enum DeliveryStatus
{
    /// <summary>Kanala gonderilmeyi bekliyor.</summary>
    Pending = 1,

    /// <summary>Kanala basariyla gonderildi.</summary>
    Delivered = 2,

    /// <summary>Tum denemeler tukendi veya kalici bir hata alindi.</summary>
    Failed = 3,

    /// <summary>Kaydedildi ama bilincli olarak gonderilmedi (ilk cekim veya cok eski haber).</summary>
    Archived = 4
}
