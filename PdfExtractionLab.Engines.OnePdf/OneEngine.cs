using OnePdf;
using OnePdf.Text;
using PdfExtractionLab.Core;

namespace PdfExtractionLab.Engines.OnePdf;

public sealed partial class OneEngine(TextOrder order = TextOrder.コンテンツ順)
    : IPdfExtractionEngine
{
    public string Name => "OnePDF";
    public string Version => Extraction.VersionOf(typeof(PdfDocument));
    public string Capabilities =>
        $"{order}。互換文字の畳み込み・合字展開・重複除去を無効化。単語・行・ブロックは OnePDF のレイアウト解析。";

    public IPdfSession Open(byte[] bytes) => new Session(PdfDocument.Open(bytes), order);

    public IPdfSession Open(string path) => new Session(PdfDocument.Open(path), order);

    private sealed partial class Session(PdfDocument doc, TextOrder order) : IPdfSession
    {
        private PdfTextOptions Options() =>
            new()
            {
                Mode =
                    order == TextOrder.コンテンツ順
                        ? PdfTextMode.ContentOrder
                        : PdfTextMode.ReadingOrder,
                DecomposeLigatures = false,
                FoldCompatibilityForms = false,
                CollapseDuplicates = false,
                RetainGlyphs = true,
                MaxDegreeOfParallelism = 1,
            };

        public int PageCount => doc.PageCount;
        private readonly HashSet<string> warnings = [];

        public string Text(int page, CancellationToken token)
        {
            var text = doc.Pages[page - 1].ExtractText(Options(), token);
            foreach (var diagnostic in text.Diagnostics)
                warnings.Add(diagnostic.ToString());
            return text.Text;
        }

        public IReadOnlyList<string> Warnings =>
            warnings.Concat(doc.Diagnostics.Select(x => x.ToString())).Distinct().ToArray();

        public void Dispose() => doc.Dispose();
    }
}
