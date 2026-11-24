using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OnePdf;
using QRCoder;

namespace PdfExtractionLab.Tests;

public static partial class Fixtures
{
    internal static readonly string[] ScanLines =
    [
        "架空処方箋",
        "検証用薬局　見本",
        "試料番号 DEMO-001",
        "品目 サンプルＡ",
        "数量 3",
    ];

    internal static (byte[] Pdf, byte[] Jpeg) JapaneseScan()
    {
        byte[] jpeg = [];
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 600, 360));
                    var face = new Typeface("MS Gothic");
                    for (int i = 0; i < ScanLines.Length; i++)
                    {
                        var line = new FormattedText(
                            ScanLines[i],
                            CultureInfo.GetCultureInfo("ja-JP"),
                            FlowDirection.LeftToRight,
                            face,
                            i == 0 ? 28 : 20,
                            Brushes.Black,
                            1
                        );
                        dc.DrawText(line, new Point(24, 20 + i * 60));
                        dc.DrawLine(
                            new Pen(Brushes.Silver, 1),
                            new Point(24, 62 + i * 60),
                            new Point(390, 62 + i * 60)
                        );
                    }
                    using var qr = QRCodeGenerator.GenerateQrCode(
                        "PDFLAB-DEMO-001",
                        QRCodeGenerator.ECCLevel.M
                    );
                    for (int y = 0; y < qr.ModuleMatrix.Count; y++)
                    for (int x = 0; x < qr.ModuleMatrix.Count; x++)
                        if (qr.ModuleMatrix[y][x])
                            dc.DrawRectangle(
                                Brushes.Black,
                                null,
                                new Rect(425 + x * 4, 145 + y * 4, 4, 4)
                            );
                }
                var bitmap = new RenderTargetBitmap(600, 360, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var ms = new MemoryStream();
                encoder.Save(ms);
                jpeg = ms.ToArray();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            throw new InvalidOperationException("合成画像を生成できません。", error);
        using var doc = PdfDocument.CreateEmpty();
        var page = doc.InsertBlankPage(0, 600, 360);
        var font = doc.AddFont(
            File.ReadAllBytes(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    "msgothic.ttc"
                )
            )
        );
        using (var b = page.BeginContent())
        {
            b.DrawImage(doc.AddJpegImage(jpeg), 0, 0, 600, 360);
            b.SetTextRenderMode(PdfTextRenderMode.Invisible);
            for (int i = 0; i < ScanLines.Length; i++)
            {
                b.SetFont(font, i == 0 ? 28 : 20);
                b.DrawText(ScanLines[i], 24, 360 - 20 - i * 60 - (i == 0 ? 25 : 18));
            }
            b.Commit();
        }
        using var output = new MemoryStream();
        doc.Save(output, new() { DocumentId = new byte[16] });
        return (output.ToArray(), jpeg);
    }
}
