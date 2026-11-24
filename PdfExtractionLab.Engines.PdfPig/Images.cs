using PdfExtractionLab.Core;
using UglyToad.PdfPig.Tokens;

namespace PdfExtractionLab.Engines.PdfPig;

public sealed partial class PigEngine
{
    private sealed partial class Session
    {
        public IReadOnlyList<EmbeddedImage> Images(int number, CancellationToken token)
        {
            var page = doc.GetPage(number);
            var result = new List<EmbeddedImage>();
            foreach (var image in page.GetImages())
            {
                token.ThrowIfCancellationRequested();
                var raw = image.RawBytes.ToArray();
                var format = EmbeddedImage.Identify(raw);
                string method = "ストリーム原本";
                var preview = raw;
                if (format == "bin" && image.TryGetPng(out var png))
                {
                    preview = png;
                    method += " / PNGへ変換";
                }
                string filter = image.ImageDictionary.TryGet(NameToken.Filter, out var f)
                    ? f.ToString() ?? "不明"
                    : "なし";
                result.Add(
                    new(
                        number,
                        result.Count + 1,
                        image.WidthInSamples,
                        image.HeightInSamples,
                        image.BitsPerComponent,
                        image.ColorSpaceDetails?.Type.ToString() ?? "不明",
                        filter,
                        format,
                        method,
                        Bounds(image.BoundingBox),
                        page.Height * Unit(page),
                        raw,
                        preview,
                        Unit(page)
                    )
                );
            }
            return result;
        }
    }
}
