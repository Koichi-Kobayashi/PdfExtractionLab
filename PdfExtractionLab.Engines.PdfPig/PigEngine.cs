using PdfExtractionLab.Core;
using UglyToad.PdfPig;

namespace PdfExtractionLab.Engines.PdfPig;

public sealed partial class PigEngine(TextOrder order = TextOrder.コンテンツ順)
    : IPdfExtractionEngine
{
    public string Name => "PdfPig";
    public string Version => Extraction.VersionOf(typeof(PdfDocument));
    public string Capabilities =>
        $"{order}。Page.Text または Docstrum＋UnsupervisedReadingOrderDetector。単語は GetWords の既定抽出器。透明性は Tr のみ、合成後の不透明度は未対応。";

    public IPdfSession Open(byte[] bytes) => Create(options => PdfDocument.Open(bytes, options));

    public IPdfSession Open(string path) => Create(options => PdfDocument.Open(path, options));

    private IPdfSession Create(Func<ParsingOptions, PdfDocument> open)
    {
        var log = new Diagnostics();
        return new Session(open(new ParsingOptions { Logger = log }), log, order);
    }

    private sealed class Diagnostics : UglyToad.PdfPig.Logging.ILog
    {
        public HashSet<string> Messages { get; } = [];

        public void Debug(string message) { }

        public void Debug(string message, Exception ex) { }

        public void Warn(string message) => Messages.Add(message);

        public void Error(string message) => Messages.Add(message);

        public void Error(string message, Exception ex) => Messages.Add(message + " " + ex.Message);
    }

    private sealed partial class Session(PdfDocument doc, Diagnostics log, TextOrder order)
        : IPdfSession
    {
        public int PageCount => doc.NumberOfPages;

        public string Text(int page, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var p = doc.GetPage(page);
            if (order == TextOrder.コンテンツ順)
                return p.Text;
            var blocks = Segment(p.GetWords());
            var ordered =
                UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector.UnsupervisedReadingOrderDetector.Instance.Get(
                    blocks
                );
            return string.Join(
                "\n\n",
                ordered.Select(b => string.Join("\n", b.TextLines.Select(l => l.Text)))
            );
        }

        public IReadOnlyList<string> Warnings => log.Messages.ToArray();

        public void Dispose() => doc.Dispose();
    }
}
