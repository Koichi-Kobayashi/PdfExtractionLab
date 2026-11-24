using PdfExtractionLab.Core;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.Tokens;

namespace PdfExtractionLab.Engines.PdfPig;

public sealed partial class PigEngine
{
    private sealed partial class Session
    {
        private static Box Bounds(PdfRectangle r)
        {
            var points = new[] { r.BottomLeft, r.BottomRight, r.TopLeft, r.TopRight };
            return new(
                points.Min(p => p.X),
                points.Min(p => p.Y),
                points.Max(p => p.X),
                points.Max(p => p.Y)
            );
        }

        private static double Unit(UglyToad.PdfPig.Content.Page page) =>
            page.Dictionary.TryGet(NameToken.UserUnit, out var value)
            && value is NumericToken n
            && n.Double > 0
                ? n.Double
                : 1;

        private static IReadOnlyList<UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock> Segment(
            IEnumerable<UglyToad.PdfPig.Content.Word> words
        ) => new DocstrumBoundingBoxes(new() { MaxDegreeOfParallelism = 1 }).GetBlocks(words);

        public PageLayout Layout(int number, CancellationToken token)
        {
            var page = doc.GetPage(number);
            var elements = new List<Element>();
            var unit = Unit(page);
            Element Row(
                string kind,
                string text,
                PdfRectangle r,
                string? font = null,
                double? size = null,
                string? direction = null,
                string? visibility = null
            ) =>
                new(
                    number,
                    kind,
                    text,
                    Bounds(r),
                    page.Height * unit,
                    font,
                    size,
                    direction,
                    visibility,
                    unit
                );
            foreach (var l in page.Letters)
            {
                token.ThrowIfCancellationRequested();
                elements.Add(
                    Row(
                        "文字",
                        l.Value,
                        l.BoundingBox,
                        l.FontName,
                        l.PointSize,
                        l.TextOrientation.ToString(),
                        $"Tr={(int)l.RenderingMode}"
                    )
                );
            }
            var words = page.GetWords().ToArray();
            elements.AddRange(
                words.Select(w =>
                    Row("単語", w.Text, w.BoundingBox, direction: w.TextOrientation.ToString())
                )
            );
            var blocks = Segment(words);
            foreach (var b in blocks)
            {
                elements.Add(Row("ブロック", b.Text, b.BoundingBox));
                elements.AddRange(b.TextLines.Select(l => Row("行", l.Text, l.BoundingBox)));
            }
            return new(
                page.Width * unit,
                page.Height * unit,
                page.Rotation.Value,
                Bounds(page.MediaBox.Bounds),
                Bounds(page.CropBox.Bounds),
                elements
            );
        }
    }
}
