using System.Globalization;
using MediaRenamer.Core.Tmdb;

namespace MediaRenamer.Core.Tests.Tmdb;

public class TmdbLanguagesTests
{
    [Theory]
    [InlineData("de-DE", "de-DE")]
    [InlineData("de-AT", "de-AT")]
    [InlineData("de", "de")]
    [InlineData("en-US", "en-US")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("zh-Hans-CN", "zh-CN")]
    [InlineData("", "en-US")]
    public void FromCulture(string culture, string expected)
    {
        Assert.Equal(expected, TmdbLanguages.FromCulture(CultureInfo.GetCultureInfo(culture)));
    }

    [Theory]
    [InlineData("de-DE", new[] { "de-DE", "en-US" })]
    [InlineData("fr", new[] { "fr", "en-US" })]
    [InlineData("en-US", new[] { "en-US" })]
    [InlineData("en-GB", new[] { "en-GB" })]
    public void SearchLanguages(string preferred, string[] expected)
    {
        Assert.Equal(expected, TmdbLanguages.SearchLanguages(preferred));
    }

    private static readonly TmdbLanguage[] Available =
    [
        new("pt-BR", "Portuguese"),
        new("de-AT", "German"),
        new("es-MX", "Spanish"),
        new("de-DE", "German"),
        new("es-ES", "Spanish"),
        new("en-GB", "English"),
        new("en-US", "English"),
        new("fr", "French"),
    ];

    [Theory]
    [InlineData("de-DE", "de-DE")] // same code
    [InlineData("de-AT", "de-AT")]
    [InlineData("DE-de", "de-DE")] // case-insensitive
    [InlineData("de", "de-DE")]    // main region of the language
    [InlineData("de-CH", "de-DE")]
    [InlineData("es-AR", "es-ES")]
    [InlineData("en", "en-US")]
    [InlineData("pt-PT", "pt-BR")] // same language in another region
    [InlineData("fr-FR", "fr")]
    [InlineData("it-IT", "en-US")] // not available: English
    public void BestMatch(string preferred, string expected)
    {
        Assert.Equal(expected, TmdbLanguages.BestMatch(Available, preferred)?.Code);
    }

    [Fact]
    public void BestMatch_NeitherPreferredNorEnglish_FirstAvailable()
    {
        Assert.Equal("ja-JP",
            TmdbLanguages.BestMatch([new TmdbLanguage("ja-JP", "Japanese"), new TmdbLanguage("ko-KR", "Korean")], "de-DE")
                ?.Code);
    }

    [Fact]
    public void BestMatch_NothingAvailable()
    {
        Assert.Null(TmdbLanguages.BestMatch([], "de-DE"));
    }

    [Fact]
    public void LanguageAndRegion()
    {
        Assert.Equal("pt", new TmdbLanguage("pt-BR", "Portuguese").Language);
        Assert.Equal("BR", new TmdbLanguage("pt-BR", "Portuguese").Region);
        Assert.Null(new TmdbLanguage("fr", "French").Region);
    }
}
