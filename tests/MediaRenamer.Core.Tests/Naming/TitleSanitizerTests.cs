using MediaRenamer.Core.Naming;

namespace MediaRenamer.Core.Tests.Naming;

public class TitleSanitizerTests
{
    [Theory]
    // examples from the requirements
    [InlineData("AC/DC", "AC_DC")]
    [InlineData("Say \"Hi\"", "Say 'Hi'")]
    [InlineData("Stop!", "Stop")]
    [InlineData("Teil 1: Der Anfang", "Teil 1 Der Anfang")]
    [InlineData("Was nun?", "Was nun")]
    [InlineData("Dr. House & Co.", "Dr. House & Co.")]
    // exclamation marks: removed at the end, otherwise replaced
    [InlineData("Go! Go!", "Go_ Go")]
    [InlineData("Help!!!", "Help")]
    [InlineData("Help ! ! ", "Help")]
    [InlineData("Really?!", "Really")]
    [InlineData("Really!?", "Really")]
    [InlineData("Yes!!! No", "Yes___ No")]
    [InlineData("!!!", "")]
    // all kinds of quotation marks
    [InlineData("It's", "It's")]
    [InlineData("It’s ‘a’ “b” „c“ «d» ‹e› ‚f‘", "It's 'a' 'b' 'c' 'd' 'e' 'f'")]
    [InlineData("Don´t `x`", "Don't 'x'")]
    // ellipsis and dashes
    [InlineData("Wait... what", "Wait… what")]
    [InlineData("Wait…", "Wait…")]
    [InlineData("A – B", "A - B")]
    [InlineData("A — B", "A - B")] // em dash
    [InlineData("Non‑breaking‐hyphen", "Non-breaking-hyphen")]
    // kept punctuation
    [InlineData("A-B.C,D'E&F(G)H…", "A-B.C,D'E&F(G)H…")]
    // characters forbidden on Windows and other special characters
    [InlineData("a<b>c|d*e\\f", "a_b_c_d_e_f")]
    [InlineData("100% #1 @home", "100_ _1 _home")]
    [InlineData("A + B = C", "A _ B _ C")]
    // letters and digits of all kinds are kept
    [InlineData("Ärger über Öl, Straße", "Ärger über Öl, Straße")]
    [InlineData("Café Olé Ça", "Café Olé Ça")]
    [InlineData("Ελληνικά Кириллица 日本語", "Ελληνικά Кириллица 日本語")]
    // whitespace
    [InlineData("A\tB", "A B")]
    [InlineData("A B", "A B")] // no-break space
    [InlineData("A   B", "A B")]
    [InlineData("  A B  ", "A B")]
    [InlineData("A : B", "A B")]
    [InlineData("", "")]
    [InlineData("?", "")]
    public void Sanitize(string title, string expected)
    {
        Assert.Equal(expected, TitleSanitizer.Sanitize(title));
    }

    [Fact]
    public void DecomposedAccentsAreComposed()
    {
        // "é" as "e" + combining acute accent
        Assert.Equal("Café", TitleSanitizer.Sanitize("Café"));
    }

    [Fact]
    public void CharactersOutsideTheBasicPlaneBecomeOneReplacement()
    {
        Assert.Equal("Party _", TitleSanitizer.Sanitize("Party 🎉"));
    }
}
