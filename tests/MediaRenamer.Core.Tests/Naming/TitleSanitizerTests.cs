using MediaRenamer.Core.Naming;

namespace MediaRenamer.Core.Tests.Naming;

public class TitleSanitizerTests
{
    [Theory]
    // examples from the requirements
    [InlineData("AC/DC", "AC_DC")]
    [InlineData("Say \"Hi\"", "Say 'Hi'")]
    [InlineData("Stop!", "Stop!")]
    [InlineData("Teil 1: Der Anfang", "Teil 1 Der Anfang")]
    [InlineData("Was nun?", "Was nun")]
    [InlineData("Dr. House & Co.", "Dr. House & Co.")]
    // exclamation marks are kept, question marks removed
    [InlineData("Go! Go!", "Go! Go!")]
    [InlineData("Help!!!", "Help!!!")]
    [InlineData("Help ! ! ", "Help ! !")]
    [InlineData("Really?!", "Really!")]
    [InlineData("Really!?", "Really!")]
    [InlineData("Yes!!! No", "Yes!!! No")]
    [InlineData("!!!", "!!!")]
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
    [InlineData("100% #1 @home", "100% #1 _home")]
    [InlineData("Episode #42", "Episode #42")]
    [InlineData("A + B = C", "A _ B _ C")]
    // letters and digits of all kinds are kept
    [InlineData("Ärger über Öl, Straße", "Ärger über Öl, Straße")]
    [InlineData("Café Olé Ça", "Café Olé Ça")]
    [InlineData("Ελληνικά Кириллица 日本語", "Ελληνικά Кириллица 日本語")]
    // whitespace
    [InlineData("A\tB", "A B")]
    [InlineData("A B", "A B")] // no-break space
    [InlineData("A   B", "A B")]
    // superscript, subscript and other special digits become normal digits
    [InlineData("E=mc²", "E_mc2")]
    [InlineData("Teil³", "Teil3")]
    [InlineData("⁰¹²³⁴⁵⁶⁷⁸⁹ ₀₁₂₃₄₅₆₇₈₉", "0123456789 0123456789")]
    [InlineData("① ⑩", "1 10")]
    [InlineData("½ ⑴ ⒈", "_ _ _")]
    // invisible format characters are removed, zero width (non-)joiners kept
    [InlineData("O \uFEFF X", "O X")] // Squid Game S02E06 on TMDB
    [InlineData("\uFEFFA\u200BB\u00AD\u200E\u2060", "AB")]
    [InlineData("می\u200Cخواهم", "می\u200Cخواهم")]
    [InlineData("👨\u200D👩", "_\u200D_")]
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
