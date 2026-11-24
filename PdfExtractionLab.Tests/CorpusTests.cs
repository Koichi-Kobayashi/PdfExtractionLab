using PdfExtractionLab.Core;
using Xunit;

namespace PdfExtractionLab.Tests;

public class CorpusTests
{
    [Fact]
    public void ScanAndOcrAreIndependent()
    {
        foreach (var engine in ExtractionTests.Engines)
        foreach (bool layer in new[] { false, true })
        {
            var result = Extraction.Run(engine, Fixtures.Scan(layer, true), default);
            Assert.Null(result.Error);
            var page = Assert.Single(result.Pages);
            Assert.Equal(layer ? "DEMO RX" : "", page.Text);
            var image = Assert.Single(page.Images);
            Assert.Equal((360, 200), (image.Width, image.Height));
            Assert.Equal(0, image.X);
            Assert.Equal(0, image.Y);
            Assert.NotEqual("bin", EmbeddedImage.Identify(image.Preview));
            if (layer)
                Assert.Contains(
                    page.Layout.Elements,
                    e => e.Kind == "文字" && e.Visibility!.Contains("Tr=3")
                );
        }
    }

    [Fact]
    public void CancelAndReleaseFile()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        try
        {
            File.WriteAllBytes(path, Fixtures.English());
            foreach (var engine in ExtractionTests.Engines)
            {
                using (var session = engine.Open(path))
                    Assert.Equal("Synthetic sample", session.Text(1, default));
                using var exclusive = File.Open(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None
                );
                Assert.ThrowsAny<OperationCanceledException>(() =>
                    Extraction.Run(engine, Fixtures.English(), new(true))
                );
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CheckSyntheticCorpus()
    {
        var root = Environment.GetEnvironmentVariable("PDFLAB_FIXTURES");
        if (root != null)
            Directory.CreateDirectory(root);
        var report = new List<string>
        {
            "| サンプル | エンジン | ページ | 原文完全一致 | 画像配置 | 解析 |",
            "|---|---|---:|---|---:|---|",
        };
        foreach (var (name, (pdf, truth)) in Fixtures.Corpus())
        {
            if (root != null)
            {
                File.WriteAllBytes(Path.Combine(root, name + ".pdf"), pdf);
                if (truth != null)
                    File.WriteAllText(Path.Combine(root, name + ".txt"), truth);
            }
            foreach (var engine in ExtractionTests.Engines)
            {
                var result = Extraction.Run(engine, pdf, default);
                if (truth == null)
                    Assert.NotNull(result.Error);
                else
                    Assert.Null(result.Error);
                bool exact = truth == result.FullText;
                if (name is "english" or "japanese-embedded" or "searchable" or "scan")
                    Assert.True(exact, $"{name} {engine.Name}: {result.FullText}");
                report.Add(
                    $"| {name} | {engine.Name} | {result.Pages.Count} | {(truth == null ? "対象外" : exact ? "一致" : "差あり")} | {result.Pages.Sum(p => p.Images.Count)} | {(result.Error?.Split(':')[0] ?? "成功")} |"
                );
                if (root != null && truth != null && !exact)
                    File.WriteAllText(
                        Path.Combine(root, name + "." + engine.Name + ".actual.txt"),
                        result.FullText
                    );
            }
        }
        if (root != null)
            File.WriteAllLines(Path.Combine(root, "observations.md"), report);
    }
}
