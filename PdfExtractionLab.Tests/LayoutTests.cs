using PdfExtractionLab.Core;
using Xunit;

namespace PdfExtractionLab.Tests;

public class LayoutTests
{
    [Fact]
    public void PreservesScalarAndWhitespaceDifferences()
    {
        Assert.Single(TextComparison.Compare("\uD800", "\uFFFD", ComparisonMode.完全一致));
        Assert.Single(TextComparison.Compare("a𠮷b", "a吉b", ComparisonMode.完全一致));
        Assert.DoesNotContain(
            TextComparison.Compare("か\u3099\r\n", "が\n", ComparisonMode.改行コードを無視),
            d => d.Removed.Contains('␍')
        );
        Assert.Empty(TextComparison.Compare("か\u3099", "が", ComparisonMode.NFC));
        Assert.NotEmpty(TextComparison.Compare("Ａ", "A", ComparisonMode.NFC));
        Assert.Empty(TextComparison.Compare("a　 b", "ab", ComparisonMode.横空白を無視));
        Assert.NotEmpty(TextComparison.Compare("a\nb", "ab", ComparisonMode.横空白を無視));
    }

    [Theory]
    [InlineData(0, 240, 220)]
    [InlineData(90, 220, 240)]
    [InlineData(180, 240, 220)]
    [InlineData(270, 220, 240)]
    public void CropAndRotationAreAppliedOnce(int rotation, double w, double h)
    {
        var bytes = Fixtures.Single(
            "BT /F1 12 Tf 50 200 Td (A) Tj ET",
            $"/CropBox [20 40 260 260] /Rotate {rotation}"
        );
        foreach (var engine in ExtractionTests.Engines)
        {
            using var doc = engine.Open(bytes);
            var layout = doc.Layout(1, default);
            Assert.Equal(w, layout.Width);
            Assert.Equal(h, layout.Height);
            var a = Assert.Single(layout.Elements, e => e.Kind == "文字");
            var (x, y) = rotation switch
            {
                0 => (30, 60),
                90 => (160, 30),
                180 => (210, 160),
                _ => (60, 210),
            };
            Assert.InRange(a.X, x - 15, x + 3);
            Assert.InRange(a.Y, y - 15, y + 3);
        }
    }
}
