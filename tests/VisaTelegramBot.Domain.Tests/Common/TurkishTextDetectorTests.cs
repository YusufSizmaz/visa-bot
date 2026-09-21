using VisaTelegramBot.Domain.Common;

namespace VisaTelegramBot.Domain.Tests.Common;

public sealed class TurkishTextDetectorTests
{
    [Theory]
    [InlineData("2026/2027 AKADEMİK YILI İÇİN İTALYAN ÜNİVERSİTELERİNE KAYIT")]
    [InlineData("İran vatandaşlarına vize verilmesi")]
    [InlineData("Presentación de solicitudes de visado –Vize başvuruları")]
    [InlineData("Vize randevu takvimi guncellendi")]
    [InlineData("Randevular")]
    public void DetectsTurkish(string text)
    {
        Assert.True(TurkishTextDetector.IsTurkish(text));
    }

    [Theory]
    [InlineData("Transition to New Visa Appointment Platform")]
    [InlineData("Appointment Scheduling FAQ")]
    [InlineData("Elecciones al Parlamento de Andalucía de 17 de mayo de 2026")]
    [InlineData("Requisitos Visado Nómadas")]
    [InlineData("Giornata della cucina italiana nel mondo")]
    [InlineData("")]
    public void RejectsOtherLanguages(string text)
    {
        Assert.False(TurkishTextDetector.IsTurkish(text));
    }

    [Fact]
    public void UsesSummaryWhenTitleIsForeign()
    {
        Assert.True(TurkishTextDetector.IsTurkish("Visa update", "Başvurular yeni sistem üzerinden alınacak."));
    }
}
