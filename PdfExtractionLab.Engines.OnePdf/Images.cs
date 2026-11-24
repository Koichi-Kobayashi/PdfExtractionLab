using OnePdf;
using PdfExtractionLab.Core;

namespace PdfExtractionLab.Engines.OnePdf;

public sealed partial class OneEngine
{
    private sealed partial class Session
    {
        public IReadOnlyList<EmbeddedImage> Images(int number, CancellationToken token)
        {
            var device = new ImageCollector(number, token);
            doc.Pages[number - 1]
                .Interpret(
                    device,
                    new() { Annotations = PdfAnnotationMode.None, GlyphOutlines = false },
                    token
                );
            return device.Items;
        }
    }

    private sealed class ImageCollector(int number, CancellationToken token) : PdfDevice
    {
        internal List<EmbeddedImage> Items { get; } = [];
        private double height;

        public override void BeginPage(in PdfPageInfo page) => height = page.Height;

        public override void DrawImage(in PdfImage image, in PdfGraphicsState state)
        {
            token.ThrowIfCancellationRequested();
            var d = image.Dictionary;
            var raw = image.GetEncodedData().ToArray();
            string format = EmbeddedImage.Identify(raw);
            string method = "ストリーム原本";
            var preview = raw;
            if (
                format == "bin"
                && image.GetData(cancellationToken: token) is { IsDecoded: true, Alpha: null } data
                && data.ColorSpace?.Name is "DeviceGray" or "DeviceRGB" or "gray" or "rgb"
            )
            {
                preview = Bitmap(
                    data.Width,
                    data.Height,
                    data.ColorSpace.ComponentCount,
                    data.Samples.Span
                );
                method += " / BMPへ変換";
            }
            var m = state.Ctm;
            var xs = new[] { m.E, m.E + m.A, m.E + m.C, m.E + m.A + m.C };
            var ys = new[] { m.F, m.F + m.B, m.F + m.D, m.F + m.B + m.D };
            // The device CTM already uses top-left output coordinates.
            var bounds = new Box(xs.Min(), ys.Min(), xs.Max(), ys.Max());
            Items.Add(
                new(
                    number,
                    Items.Count + 1,
                    (int)d.GetInt("Width"),
                    (int)d.GetInt("Height"),
                    (int)d.GetInt("BitsPerComponent"),
                    d.GetName("ColorSpace") ?? d["ColorSpace"]?.ToString() ?? "不明",
                    image.Stream is { } stream
                        ? string.Join("+", stream.FilterNames)
                        : d["Filter"]?.ToString() ?? "なし",
                    format,
                    method,
                    bounds,
                    height,
                    raw,
                    preview,
                    RawTopDown: true
                )
            );
        }

        private static byte[] Bitmap(
            int width,
            int height,
            int components,
            ReadOnlySpan<byte> samples
        )
        {
            int stride = checked((width * 3 + 3) & ~3);
            using var ms = new MemoryStream(checked(54 + stride * height));
            using var w = new BinaryWriter(ms);
            w.Write((ushort)0x4d42);
            w.Write(ms.Capacity);
            w.Write(0);
            w.Write(54);
            w.Write(40);
            w.Write(width);
            w.Write(-height);
            w.Write((ushort)1);
            w.Write((ushort)24);
            w.Write(0);
            w.Write(stride * height);
            w.Write(2835);
            w.Write(2835);
            w.Write(0);
            w.Write(0);
            byte[] row = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * components;
                    row[3 * x] = samples[i + (components == 3 ? 2 : 0)];
                    row[3 * x + 1] = samples[i + (components == 3 ? 1 : 0)];
                    row[3 * x + 2] = samples[i];
                }
                w.Write(row);
            }
            return ms.ToArray();
        }
    }
}
