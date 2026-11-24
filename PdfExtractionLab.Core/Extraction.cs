using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace PdfExtractionLab.Core;

public enum TextOrder
{
    コンテンツ順,
    読み取り順,
}

public interface IPdfExtractionEngine
{
    string Name { get; }
    string Version { get; }
    string Capabilities { get; }
    IPdfSession Open(byte[] bytes);
    IPdfSession Open(string path);
}

public interface IPdfSession : IDisposable
{
    int PageCount { get; }
    string Text(int page, CancellationToken token);
    PageLayout Layout(int page, CancellationToken token);
    IReadOnlyList<EmbeddedImage> Images(int page, CancellationToken token);
    IReadOnlyList<string> Warnings { get; }
}

public record Box(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;
    public double Height => Top - Bottom;
}

public record Element(
    int Page,
    string Kind,
    string Text,
    Box Raw,
    double PageHeight,
    string? Font = null,
    double? Size = null,
    string? Direction = null,
    string? Visibility = null,
    double Unit = 1
)
{
    public double X => Raw.Left * Unit;
    public double Y => PageHeight - Raw.Top * Unit;
    public double Width => Raw.Width * Unit;
    public double Height => Raw.Height * Unit;
}

public record PageLayout(
    double Width,
    double Height,
    int Rotation,
    Box Media,
    Box Crop,
    IReadOnlyList<Element> Elements
);

public record EmbeddedImage(
    int Page,
    int Index,
    int Width,
    int Height,
    int Bits,
    string ColorSpace,
    string Filter,
    string Format,
    string Method,
    Box? Raw,
    double PageHeight,
    byte[] Data,
    byte[] Preview,
    double Unit = 1,
    bool RawTopDown = false
)
{
    public long Pixels => (long)Width * Height;
    public double? X => Raw?.Left * Unit;
    public double? Y => RawTopDown ? Raw?.Bottom : PageHeight - Raw?.Top * Unit;

    public static string Identify(ReadOnlySpan<byte> data) =>
        data switch
        {
            [0xff, 0xd8, 0xff, ..] => "jpg",
            [137, 80, 78, 71, 13, 10, 26, 10, ..] => "png",
            [66, 77, ..] => "bmp",
            [0, 0, 0, 12, 106, 80, 32, 32, ..] => "jp2",
            _ => "bin",
        };

    public override string ToString() =>
        $"p.{Page} / #{Index}   {Width} × {Height}   {Format}   {Method}";
}

public record PageResult(
    int Number,
    string Text,
    PageLayout Layout,
    IReadOnlyList<EmbeddedImage> Images
);

public record Timing(double Open, double Text, double Layout, double Images, double Total);

public record Result(
    string Engine,
    string Version,
    string Capabilities,
    IReadOnlyList<PageResult> Pages,
    Timing Times,
    IReadOnlyList<string> Warnings,
    string? Error
)
{
    public string FullText => string.Join("\f", Pages.Select(p => p.Text));
    public string Summary =>
        Error
        ?? $"成功　{Pages.Count} ページ　{Pages.Sum(p => p.Text.Length)} UTF-16　{Pages.Sum(p => p.Text.EnumerateRunes().Count())} スカラー\n"
            + $"{Pages.Sum(p => p.Layout.Elements.Count(e => e.Kind == "単語"))} 単語　{Pages.Sum(p => p.Text.Length == 0 ? 0 : p.Text.ReplaceLineEndings("\n").Count(c => c == '\n') + 1)} 行　画像 {Pages.Sum(p => p.Images.Count)} 件\n"
            + $"開く {Times.Open:F2} ms　テキスト {Times.Text:F2} ms　合計 {Times.Total:F2} ms\n"
            + $"座標 {Times.Layout:F2} ms　画像 {Times.Images:F2} ms";
}

public static class Extraction
{
    public static string VersionOf(Type type) =>
        type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0]
        ?? type.Assembly.GetName().Version!.ToString();

    public static Result Run(
        IPdfExtractionEngine engine,
        byte[] bytes,
        CancellationToken token,
        Action<int, int>? progress = null
    )
    {
        List<PageResult> pages = [];
        List<string> warnings = [];
        double open = 0,
            text = 0,
            layout = 0,
            images = 0;
        string? error = null;
        try
        {
            token.ThrowIfCancellationRequested();
            var clock = Stopwatch.StartNew();
            using var session = engine.Open(bytes);
            open = clock.Elapsed.TotalMilliseconds;
            int count = session.PageCount;
            for (int i = 1; i <= count; ++i)
            {
                token.ThrowIfCancellationRequested();
                clock.Restart();
                var raw = session.Text(i, token);
                text += clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                var positions = session.Layout(i, token);
                layout += clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                var pictures = session.Images(i, token);
                images += clock.Elapsed.TotalMilliseconds;
                pages.Add(new(i, raw, positions, pictures));
                progress?.Invoke(i, count);
            }
            warnings.AddRange(session.Warnings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
        }
        return new(
            engine.Name,
            engine.Version,
            engine.Capabilities,
            pages,
            new(open, text, layout, images, open + text + layout + images),
            warnings,
            error
        );
    }
}
