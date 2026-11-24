using PdfExtractionLab.Core;
using PdfExtractionLab.Engines.OnePdf;
using PdfExtractionLab.Engines.PdfPig;
using Xunit;

namespace PdfExtractionLab.Tests;

public class AdditionalCoverage
{
    [Fact]
    public void ReadingOrderOperatesOnPageLayout()
    {
        IPdfExtractionEngine[] engines =
        [
            new PigEngine(TextOrder.読み取り順),
            new OneEngine(TextOrder.読み取り順),
        ];
        byte[] bytes = Fixtures.Single("BT /F1 12 Tf 50 120 Td (Lower) Tj 0 80 Td (Upper) Tj ET");
        foreach (var engine in engines)
        {
            using var doc = engine.Open(bytes);
            string s = doc.Text(1, default);
            Assert.True(
                s.IndexOf("Upper", StringComparison.Ordinal)
                    < s.IndexOf("Lower", StringComparison.Ordinal),
                s
            );
        }
    }

    [Fact]
    public void UserUnitsScaleToPhysicalPoints()
    {
        var normal = Fixtures.Single(
            "BT /F1 12 Tf 50 200 Td (A) Tj ET",
            "/CropBox [20 40 260 260] /Rotate 90"
        );
        var scaled = Fixtures.Single(
            "BT /F1 12 Tf 50 200 Td (A) Tj ET",
            "/CropBox [20 40 260 260] /Rotate 90 /UserUnit 2"
        );
        foreach (var engine in ExtractionTests.Engines)
        {
            using var a = engine.Open(normal);
            using var b = engine.Open(scaled);
            var first = a.Layout(1, default);
            var second = b.Layout(1, default);
            Assert.Equal(first.Height * 2, second.Height, 4);
            var x = Assert.Single(first.Elements, e => e.Kind == "文字");
            var y = Assert.Single(second.Elements, e => e.Kind == "文字");
            Assert.Equal(x.X * 2, y.X, 4);
            Assert.Equal(x.Y * 2, y.Y, 4);
        }
    }

    [Fact]
    public void FileModeCanReopenAndCountFailures()
    {
        var temp = Path.GetTempFileName();
        try
        {
            foreach (var e in ExtractionTests.Engines)
            {
                File.WriteAllBytes(temp, Fixtures.English());
                using (
                    var guard = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read)
                )
                {
                    var rows = BenchmarkWork.Measure(
                        e,
                        temp,
                        [],
                        new(InputSource.File, Lifetime.Reopen, 2, 1, false),
                        default
                    );
                    Assert.All(rows, r => Assert.Equal(2, r.Success));
                }
                File.WriteAllBytes(temp, [0, 1, 2]);
                var failed = BenchmarkWork.Measure(
                    e,
                    temp,
                    [],
                    new(InputSource.File, Lifetime.Reuse, 2),
                    default
                );
                Assert.All(
                    failed,
                    r =>
                    {
                        Assert.Equal(2, r.Failure);
                        Assert.Null(r.MeanMs);
                    }
                );
            }
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
