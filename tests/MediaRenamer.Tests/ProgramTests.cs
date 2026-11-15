using Avalonia.Styling;

namespace MediaRenamer.Tests;

public class ProgramTests
{
    [Theory]
    [InlineData(new[] { "--theme=dark" }, "Dark")]
    [InlineData(new[] { "--theme=Light", "/shows/Eureka" }, "Light")]
    [InlineData(new[] { "--THEME=DARK" }, "Dark")]
    [InlineData(new[] { "--theme=light", "--theme=dark" }, "Dark")] // the last one wins
    [InlineData(new[] { "--theme=blue" }, null)]
    [InlineData(new[] { "/shows/Eureka" }, null)]
    [InlineData(new string[0], null)]
    public void ParseTheme(string[] arguments, string? expected)
    {
        Assert.Equal(expected, Program.ParseTheme(arguments)?.Key.ToString());
    }

    [Fact]
    public void ParseTheme_ReturnsTheBuiltInVariants()
    {
        Assert.Same(ThemeVariant.Dark, Program.ParseTheme(["--theme=dark"]));
    }
}
