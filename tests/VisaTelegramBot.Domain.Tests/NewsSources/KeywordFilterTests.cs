using VisaTelegramBot.Domain.Common;
using VisaTelegramBot.Domain.NewsSources;

namespace VisaTelegramBot.Domain.Tests.NewsSources;

public sealed class KeywordFilterTests
{
    private static readonly KeywordFilter Filter = KeywordFilter.CreateOrNull(
        ["randevu", "vize başvuru", "cita", "visado", "appointment", "BLS"])!;

    [Theory]
    [InlineData("ÖĞRENCİ VİZESİ BAŞVURUSU RANDEVULARININ ALINMASINA İLİŞKİN")]
    [InlineData("Randevu sistemi güncellendi")]
    [InlineData("Presentación de solicitudes de visado")]
    [InlineData("Nuevas citas disponibles")]
    [InlineData("Transition to New Visa Appointment Platform")]
    [InlineData("BLS merkezinde yeni düzenleme")]
    [InlineData("Vize başvuruları hakkında duyuru")]
    public void Matches_TurkishSpanishEnglishVariants(string title)
    {
        Assert.True(Filter.Matches(title, null));
    }

    [Theory]
    [InlineData("Felicitaciones por el día nacional")]
    [InlineData("Elecciones al Parlamento de Andalucía")]
    [InlineData("Başvuru formu yayınlandı")]
    [InlineData("İtalyan Mutfağı Haftası")]
    public void DoesNotMatch_UnrelatedOrPartialWords(string title)
    {
        Assert.False(Filter.Matches(title, null));
    }

    [Fact]
    public void Matches_SearchesSummaryToo()
    {
        Assert.True(Filter.Matches("Önemli duyuru", "Randevular 1 Ekim'de açılacak."));
    }

    [Fact]
    public void CreateOrNull_WithoutKeywords_ReturnsNull()
    {
        Assert.Null(KeywordFilter.CreateOrNull(null));
        Assert.Null(KeywordFilter.CreateOrNull([]));
        Assert.Null(KeywordFilter.CreateOrNull(["  ", ""]));
    }

    [Fact]
    public void CreateOrNull_RemovesDuplicatesIgnoringCase()
    {
        var filter = KeywordFilter.CreateOrNull(["Randevu", "randevu", " RANDEVU "])!;

        Assert.Single(filter.Keywords);
    }

    [Fact]
    public void CreateOrNull_WithTooManyKeywords_Throws()
    {
        var keywords = Enumerable.Range(0, KeywordFilter.MaxKeywords + 1).Select(i => $"kelime{i}");

        Assert.Throws<DomainException>(() => KeywordFilter.CreateOrNull(keywords));
    }

    [Fact]
    public void NewsSource_WithoutFilter_TreatsEverythingAsRelevant()
    {
        var source = TestData.RssSource();

        Assert.True(source.IsRelevant("Herhangi bir başlık", null));
    }

    [Fact]
    public void NewsSource_WithFilter_UsesIt()
    {
        var source = TestData.RssSource();
        source.ChangeKeywordFilter(Filter);

        Assert.True(source.IsRelevant("Randevu takvimi", null));
        Assert.False(source.IsRelevant("Kültür festivali", null));
    }
}
