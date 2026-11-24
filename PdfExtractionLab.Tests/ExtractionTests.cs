using System.Text;
using PdfExtractionLab.Core;
using PdfExtractionLab.Engines.OnePdf;
using PdfExtractionLab.Engines.PdfPig;
using Xunit;

namespace PdfExtractionLab.Tests;

public class ExtractionTests
{
    internal static IPdfExtractionEngine[] Engines => [new PigEngine(), new OneEngine()];

    [Fact]
    public void RejectsUnreadableInputIndependently()
    {
        foreach (var e in Engines)
            Assert.NotNull(
                Extraction.Run(e, Encoding.ASCII.GetBytes("invalid input"), default).Error
            );
    }

    [Fact]
    public void ExtractsEnglishWithoutChangingInput()
    {
        var bytes = Fixtures.English();
        var original = bytes.ToArray();
        foreach (var e in Engines)
        {
            var result = Extraction.Run(e, bytes, default);
            Assert.Null(result.Error);
            Assert.Equal("Synthetic sample", Assert.Single(result.Pages).Text);
        }
        Assert.Equal(original, bytes);
    }
}
