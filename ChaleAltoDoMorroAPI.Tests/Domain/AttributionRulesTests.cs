using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Origem da pré-reserva (UTM). Regra de ouro: nada aqui é recusado, só limpo (landing: src/lib/attribution.ts).
public class AttributionRulesTests
{
    [Theory]
    [InlineData("meta_ads", "meta_ads")]
    [InlineData("instagram", "instagram")]
    [InlineData("google", "google")]
    [InlineData("direct", "direct")]
    [InlineData("other", "other")]
    [InlineData(" Google ", "google")]   // espaços e maiúsculas não importam
    [InlineData("tiktok", "other")]      // fora da lista
    [InlineData("", "other")]
    [InlineData(null, "other")]
    public void Source_fora_da_lista_vira_other(string? source, string expected) =>
        Assert.Equal(expected, AttributionRules.NormalizeSource(source));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  inverno-2026 ", "inverno-2026")]
    public void Utm_tira_espacos_e_vazio_vira_null(string? text, string? expected) =>
        Assert.Equal(expected, AttributionRules.CleanText(text));

    [Fact]
    public void Utm_longo_eh_cortado_em_150()
    {
        var cleaned = AttributionRules.CleanText(new string('a', 400));

        Assert.Equal(AttributionRules.MaxUtmLength, cleaned!.Length);
    }

    [Fact]
    public void Fbclid_aceita_ate_500()
    {
        var fbclid = new string('f', 300);

        Assert.Equal(fbclid, AttributionRules.Normalize(null, null, null, null, null, null, fbclid, null).Fbclid);
        Assert.Equal(AttributionRules.MaxFbclidLength, AttributionRules.CleanText(new string('f', 900), AttributionRules.MaxFbclidLength)!.Length);
    }

    [Fact]
    public void Data_iso_em_utc_eh_mantida()
    {
        var parsed = AttributionRules.ParseUtc("2026-10-05T14:30:00.000Z");

        Assert.Equal(new DateTimeOffset(2026, 10, 5, 14, 30, 0, TimeSpan.Zero), parsed);
        Assert.Equal(TimeSpan.Zero, parsed!.Value.Offset);
    }

    [Fact]
    public void Data_com_fuso_eh_convertida_para_utc()
    {
        var parsed = AttributionRules.ParseUtc("2026-10-05T11:30:00-03:00");

        Assert.Equal(new DateTimeOffset(2026, 10, 5, 14, 30, 0, TimeSpan.Zero), parsed);
        Assert.Equal(TimeSpan.Zero, parsed!.Value.Offset); // o Npgsql só grava timestamptz com offset zero
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ontem")]
    [InlineData("2026-13-45T99:00:00Z")]
    public void Data_invalida_vira_null(string? text) =>
        Assert.Null(AttributionRules.ParseUtc(text));

    [Fact]
    public void Normalize_junta_tudo()
    {
        var attribution = AttributionRules.Normalize(
            "meta_ads", " meta ", "paid", "inverno-2026", "", " casais-poa ", "IwAR0abc", "2026-10-05T14:30:00.000Z");

        Assert.Equal(
            new ReservationAttribution("meta_ads", "meta", "paid", "inverno-2026", null, "casais-poa", "IwAR0abc",
                new DateTimeOffset(2026, 10, 5, 14, 30, 0, TimeSpan.Zero)),
            attribution);
    }
}
