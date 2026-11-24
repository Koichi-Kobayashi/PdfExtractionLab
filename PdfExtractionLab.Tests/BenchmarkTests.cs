using PdfExtractionLab.Core;
using Xunit;

namespace PdfExtractionLab.Tests;

public class BenchmarkTests
{
    [Theory]
    [InlineData(Lifetime.Reopen, 1)]
    [InlineData(Lifetime.Reuse, 2)]
    public void CountsSuccessfulSamples(Lifetime life, int workers)
    {
        var e = ExtractionTests.Engines[0];
        var rows = BenchmarkWork.Measure(
            e,
            "",
            Fixtures.English(),
            new(InputSource.Memory, life, 2, workers),
            default
        );
        Assert.Equal(5, rows.Count);
        Assert.All(
            rows,
            r =>
            {
                Assert.Equal(2, r.Success);
                Assert.Equal(0, r.Failure);
                Assert.True(r.P95Ms >= r.MinMs);
            }
        );
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".json")]
    [InlineData(".md")]
    [InlineData(".csv")]
    public void ExportsOnlyExplicitContent(string extension)
    {
        const string secret = "private-marker";
        var r = Extraction.Run(ExtractionTests.Engines[0], Fixtures.English(), default) with
        {
            Warnings = [secret],
        };
        var safe = Reports.Write(extension, [r], DateTimeOffset.Now, null, false);
        Assert.DoesNotContain(secret, safe);
        Assert.DoesNotContain("Synthetic sample", safe);
        var full = Reports.Write(extension, [r], DateTimeOffset.Now, null, true);
        Assert.Contains(secret, full);
        Assert.Contains("Synthetic sample", full);
        Assert.Contains("CPU", safe);
    }
}
