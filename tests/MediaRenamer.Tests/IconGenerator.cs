using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace MediaRenamer.Tests;

/// <summary>
/// Generates the application icon from a single vector description: <c>icon.svg</c>, <c>icon.ico</c> (all sizes
/// rendered as vectors) and <c>icon-256.png</c> in <c>src/MediaRenamer/Assets</c>.
/// Run after changing the icon: <c>dotnet test --project tests/MediaRenamer.Tests -- --explicit only --filter-class *IconGenerator</c>.
/// </summary>
public sealed class IconGenerator
{
    private const int Canvas = 256;
    private static readonly int[] IcoSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    private const string GradientStart = "#6D28D9";
    private const string GradientEnd = "#EC4899";
    private const string Dark = "#111827";
    private const string Light = "#F8FAFC";

    /// <summary>A shape: SVG path data, fill (a color or <c>gradient</c>) and an optional rotation around a point.</summary>
    private sealed record Shape(string Path, string Fill, double Rotation = 0, double CenterX = 0, double CenterY = 0);

    /// <summary>The icon: a clapperboard whose board shows two lines of text and a text caret.</summary>
    private static IEnumerable<Shape> Icon()
    {
        // background
        yield return new Shape(RoundedRect(12, 12, 232, 232, 52), "gradient");

        // clapper stick with stripes, tilted around the hinge
        const double angle = -12, hingeX = 50, hingeY = 106;
        yield return new Shape(RoundedRect(50, 80, 156, 26, 6), Dark, angle, hingeX, hingeY);
        for (var i = 0; i < 4; i++)
        {
            var x = Format(70 + i * 36);
            yield return new Shape($"M {x},80 h 16 l -11,26 h -16 Z", Light, angle, hingeX, hingeY);
        }

        // board
        yield return new Shape(RoundedRect(50, 110, 156, 98, 14), Light);
        // hinge
        yield return new Shape(Circle(56, 110, 9), Dark);
        // text lines and caret
        yield return new Shape(RoundedRect(70, 134, 84, 16, 8), GradientStart);
        yield return new Shape(RoundedRect(70, 166, 110, 16, 8), "#A1A1AA");
        yield return new Shape(RoundedRect(164, 126, 10, 32, 4), GradientEnd);
    }

    [Fact(Explicit = true)]
    public void Generate()
    {
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var assets = Path.Combine(RepositoryRoot(), "src", "MediaRenamer", "Assets");
        Directory.CreateDirectory(assets);

        File.WriteAllText(Path.Combine(assets, "icon.svg"), Svg());

        var pngs = IcoSizes.Select(size => (size, Png: Render(size))).ToList();
        File.WriteAllBytes(Path.Combine(assets, "icon.ico"), Ico(pngs));
        File.WriteAllBytes(Path.Combine(assets, "icon-256.png"), pngs.Single(p => p.size == 256).Png);

        // preview of all sizes, for checking the result
        var preview = Environment.GetEnvironmentVariable("SCREENSHOT_DIR");
        if (preview is not null)
        {
            Directory.CreateDirectory(preview);
            File.WriteAllBytes(Path.Combine(preview, "icon-preview.png"), PreviewSheet(pngs));
        }
    }

    private static byte[] Render(int size)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        using (var ctx = bitmap.CreateDrawingContext())
        using (ctx.PushTransform(Matrix.CreateScale(size / (double)Canvas, size / (double)Canvas)))
        {
            foreach (var shape in Icon())
            {
                var brush = shape.Fill == "gradient"
                    ? new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops =
                        {
                            new GradientStop(Color.Parse(GradientStart), 0),
                            new GradientStop(Color.Parse(GradientEnd), 1),
                        },
                    }
                    : (IBrush)new SolidColorBrush(Color.Parse(shape.Fill));
                var transform = Matrix.CreateTranslation(-shape.CenterX, -shape.CenterY)
                                * Matrix.CreateRotation(shape.Rotation * Math.PI / 180)
                                * Matrix.CreateTranslation(shape.CenterX, shape.CenterY);
                using (ctx.PushTransform(transform))
                {
                    ctx.DrawGeometry(brush, null, Geometry.Parse(shape.Path));
                }
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }

    private static string Svg()
    {
        var svg = new StringBuilder();
        svg.AppendLine($"""<svg xmlns="http://www.w3.org/2000/svg" width="{Canvas}" height="{Canvas}" viewBox="0 0 {Canvas} {Canvas}">""");
        svg.AppendLine("  <!-- generated by tests/MediaRenamer.Tests/IconGenerator.cs, do not edit -->");
        svg.AppendLine("  <defs>");
        svg.AppendLine("    <linearGradient id=\"background\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">");
        svg.AppendLine($"      <stop offset=\"0\" stop-color=\"{GradientStart}\"/>");
        svg.AppendLine($"      <stop offset=\"1\" stop-color=\"{GradientEnd}\"/>");
        svg.AppendLine("    </linearGradient>");
        svg.AppendLine("  </defs>");
        foreach (var shape in Icon())
        {
            var fill = shape.Fill == "gradient" ? "url(#background)" : shape.Fill;
            var transform = shape.Rotation == 0
                ? string.Empty
                : $" transform=\"rotate({Format(shape.Rotation)} {Format(shape.CenterX)} {Format(shape.CenterY)})\"";
            svg.AppendLine($"  <path d=\"{shape.Path}\" fill=\"{fill}\"{transform}/>");
        }

        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    /// <summary>ICO file with PNG compressed images (supported since Windows Vista).</summary>
    private static byte[] Ico(IReadOnlyList<(int Size, byte[] Png)> images)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); // reserved
        writer.Write((ushort)1); // type: icon
        writer.Write((ushort)images.Count);
        var offset = 6 + 16 * images.Count;
        foreach (var (size, png) in images)
        {
            writer.Write((byte)(size >= 256 ? 0 : size)); // width, 0 means 256
            writer.Write((byte)(size >= 256 ? 0 : size)); // height
            writer.Write((byte)0); // no palette
            writer.Write((byte)0); // reserved
            writer.Write((ushort)1); // color planes
            writer.Write((ushort)32); // bits per pixel
            writer.Write(png.Length);
            writer.Write(offset);
            offset += png.Length;
        }

        foreach (var (_, png) in images)
        {
            writer.Write(png);
        }

        return stream.ToArray();
    }

    private static byte[] PreviewSheet(IReadOnlyList<(int Size, byte[] Png)> images)
    {
        var width = images.Sum(i => i.Size + 16) + 16;
        using var sheet = new RenderTargetBitmap(new PixelSize(width, 256 + 32));
        using (var ctx = sheet.CreateDrawingContext())
        {
            ctx.FillRectangle(Brushes.White, new Rect(0, 0, width, 256 + 32));
            double x = 16;
            foreach (var (size, png) in images.OrderByDescending(i => i.Size))
            {
                using var bitmap = new Bitmap(new MemoryStream(png));
                ctx.DrawImage(bitmap, new Rect(x, 16, size, size));
                x += size + 16;
            }
        }

        using var stream = new MemoryStream();
        sheet.Save(stream, new PngBitmapEncoderOptions());
        return stream.ToArray();
    }

    private static string RoundedRect(double x, double y, double width, double height, double radius) =>
        string.Create(CultureInfo.InvariantCulture,
            $"M {x + radius},{y} H {x + width - radius} A {radius},{radius} 0 0 1 {x + width},{y + radius} " +
            $"V {y + height - radius} A {radius},{radius} 0 0 1 {x + width - radius},{y + height} " +
            $"H {x + radius} A {radius},{radius} 0 0 1 {x},{y + height - radius} " +
            $"V {y + radius} A {radius},{radius} 0 0 1 {x + radius},{y} Z");

    private static string Circle(double cx, double cy, double r) =>
        string.Create(CultureInfo.InvariantCulture,
            $"M {cx - r},{cy} A {r},{r} 0 1 1 {cx + r},{cy} A {r},{r} 0 1 1 {cx - r},{cy} Z");

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MediaRenamer.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
