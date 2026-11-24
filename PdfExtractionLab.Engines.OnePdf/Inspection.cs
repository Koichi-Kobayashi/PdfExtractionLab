using OnePdf;
using PdfExtractionLab.Core;

namespace PdfExtractionLab.Engines.OnePdf;

public sealed partial class OneEngine
{
    private sealed partial class Session
    {
        private static Box Bounds(PdfRectangle r) => new(r.Left, r.Bottom, r.Right, r.Top);

        public PageLayout Layout(int number, CancellationToken token)
        {
            var page = doc.Pages[number - 1];
            var text = page.ExtractText(Options(), token);
            List<Element> elements = [];
            foreach (var g in text.Glyphs)
            {
                token.ThrowIfCancellationRequested();
                elements.Add(
                    new(
                        number,
                        "文字",
                        g.Text,
                        Bounds(g.Bounds),
                        text.Height,
                        g.FontName,
                        g.Size,
                        g.IsVertical
                            ? "縦"
                            : $"{Math.Atan2(g.Matrix.B, g.Matrix.A) * 180 / Math.PI:F1}°",
                        $"visible={g.IsVisible}, Tr={g.RenderMode}"
                    )
                );
            }
            foreach (var block in text.Blocks)
            {
                elements.Add(
                    new(
                        number,
                        "ブロック",
                        string.Join("\n", block.Lines.Select(l => l.Text)),
                        Bounds(block.Bounds),
                        text.Height
                    )
                );
                foreach (var line in block.Lines)
                {
                    elements.Add(
                        new(
                            number,
                            "行",
                            line.Text,
                            Bounds(line.Bounds),
                            text.Height,
                            Direction: line.Direction.ToString()
                        )
                    );
                    elements.AddRange(
                        line.Words.Select(w => new Element(
                            number,
                            "単語",
                            w.Text,
                            Bounds(w.Bounds),
                            text.Height,
                            Visibility: w.IsVisible.ToString()
                        ))
                    );
                }
            }
            return new(
                text.Width,
                text.Height,
                page.Rotate,
                Bounds(page.MediaBox),
                Bounds(page.CropBox),
                elements
            );
        }
    }
}
