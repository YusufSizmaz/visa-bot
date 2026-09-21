namespace VisaTelegramBot.Domain.NewsItems;

/// <summary>Bir haberin neden kanala gonderilmedigi. Panelde "neden gitmedi?" sorusunu cevaplar.</summary>
public enum ArchiveReason
{
    /// <summary>Kaynagin ilk taramasi; eski haberler kanala dokulmez.</summary>
    InitialImport = 1,

    /// <summary>Yayin tarihi cok eski.</summary>
    TooOld = 2,

    /// <summary>Anahtar kelime filtresine uymadi.</summary>
    NotRelevant = 3,

    /// <summary>Konu ilgili ama metin Turkce degil.</summary>
    NotTurkish = 4,

    /// <summary>Tek taramada kuyruga alinabilecek haber siniri asildi.</summary>
    QueueLimit = 5
}
