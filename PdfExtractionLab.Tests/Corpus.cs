using System.IO.Compression;
using System.Text;
using OnePdf;
using QRCoder;

namespace PdfExtractionLab.Tests;

public static partial class Fixtures
{
    public const string Japanese = "処方箋 ひらがな カタカナ ＡＢＣ ｶﾅ 髙";
    public const string Rare = "𠮷葛\U000E0100\uE001";

    public static Dictionary<string, (byte[] Pdf, string? Truth)> Corpus()
    {
        var items = new Dictionary<string, (byte[], string?)>();
        items["english"] = (English(), "Synthetic sample");
        items["japanese-embedded"] = (Embedded(Japanese, false), Japanese);
        items["vertical"] = (Embedded("縦書き見本", true), "縦書き見本");
        items["unicode-map"] = (UnicodeProbe(), Rare);
        items["table"] = (
            Single(
                "30 170 220 60 re S 30 200 m 250 200 l S 140 170 m 140 230 l S BT /F1 12 Tf 40 211 Td (Item) Tj 110 0 Td (Count) Tj -110 -30 Td (Demo) Tj 110 0 Td (2) Tj ET"
            ),
            "Item Count\nDemo 2"
        );
        items["scan"] = (Scan(false, false), "");
        items["searchable"] = (Scan(true, false), "DEMO RX");
        items["fictional-rx"] = (
            Embedded("架空処方箋　検証用見本", false),
            "架空処方箋　検証用見本"
        );
        items["fictional-qr"] = (Scan(true, true), "DEMO RX");
        items["japanese-ocr-jpeg"] = (JapaneseScan().Pdf, string.Join("\n", ScanLines));
        items["rotated"] = (
            Single("BT /F1 14 Tf 50 180 Td (Sample) Tj ET", "/CropBox [20 40 260 260] /Rotate 90"),
            "Sample"
        );
        items["many-pages"] = (Many(300), string.Join('\f', Enumerable.Repeat("Demo", 300)));
        items["damaged"] = (
            Encoding.ASCII.GetBytes("%PDF-1.7\n% deliberately incomplete fixture\n"),
            null
        );
        using var doc = PdfDocument.Open(English());
        using var encrypted = new MemoryStream();
        doc.Save(
            encrypted,
            new()
            {
                Encryption = PdfEncryptionAction.Set,
                NewEncryption = new() { UserPassword = "sample-only" },
            }
        );
        items["password"] = (encrypted.ToArray(), null);
        return items;
    }

    private static byte[] Embedded(string text, bool vertical)
    {
        using var doc = PdfDocument.CreateEmpty();
        var page = doc.InsertBlankPage(0, 420, 300);
        string font = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            "msgothic.ttc"
        );
        var face = doc.AddFont(File.ReadAllBytes(font));
        using (var content = page.BeginContent())
        {
            content.SetFont(face, 15);
            content.DrawText(text, vertical ? 320 : 24, 235);
            content.Commit();
        }
        using var ms = new MemoryStream();
        doc.Save(
            ms,
            new()
            {
                DocumentId = new byte[16],
                XrefFormat = PdfXrefFormat.Table,
                UseObjectStreams = false,
            }
        );
        byte[] data = ms.ToArray();
        if (vertical)
        {
            int offset = data.AsSpan().IndexOf("/Identity-H"u8);
            if (offset < 0)
                throw new InvalidDataException("Type0 encoding missing");
            data[offset + 10] = (byte)'V';
        }
        return data;
    }

    private static byte[] UnicodeProbe()
    {
        // Each source code deliberately maps to one scalar or an IVS sequence.
        var map =
            "/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def /CMapName /Probe def /CMapType 2 def 1 begincodespacerange <00> <ff> endcodespacerange 3 beginbfchar <41> <D842DFB7> <42> <845BDB40DD00> <43> <E001> endbfchar endcmap CMapName currentdict /CMap defineresource pop end end";
        return Document([
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /ToUnicode 6 0 R >>",
            Stream("BT /F1 16 Tf 30 240 Td (ABC) Tj ET"),
            Stream(map),
        ]);
    }

    internal static byte[] Large() => Many(300);

    private static byte[] Many(int count)
    {
        var kids = string.Join(" ", Enumerable.Range(5, count).Select(i => $"{i} 0 R"));
        List<string> objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Count {count} /Kids [{kids}] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream("BT /F1 16 Tf 20 240 Td (Demo) Tj ET"),
        ];
        objects.AddRange(
            Enumerable.Repeat(
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 3 0 R >> >> /Contents 4 0 R >>",
                count
            )
        );
        return Document(objects);
    }

    internal static byte[] Scan(bool ocr, bool qr)
    {
        const int width = 360,
            height = 200;
        var pixels = Enumerable.Repeat((byte)255, width * height).ToArray();
        void Rect(int x, int y, int w, int h, byte v = 32)
        {
            for (int row = y; row < y + h; row++)
                pixels.AsSpan(row * width + x, w).Fill(v);
        }
        var letters = new[]
        {
            "110|101|101|101|110",
            "111|100|110|100|111",
            "10001|11011|10101|10001|10001",
            "111|101|101|101|111",
            "",
            "110|101|110|101|101",
            "101|101|010|101|101",
        };
        int left = 18;
        foreach (var glyph in letters)
        {
            var rows = glyph.Split('|');
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == '1')
                    Rect(left + 3 * x, 18 + 3 * y, 3, 3);
            left += (rows[0].Length + 1) * 3;
        }
        Rect(18, 47, 320, 2);
        Rect(18, 75, 175, 1, 140);
        Rect(18, 108, 175, 1, 140);
        Rect(18, 141, 175, 1, 140);
        if (qr)
        {
            using var symbol = QRCodeGenerator.GenerateQrCode(
                "SYNTHETIC-RX-001",
                QRCodeGenerator.ECCLevel.M
            );
            for (int y = 0; y < symbol.ModuleMatrix.Count; y++)
            for (int x = 0; x < symbol.ModuleMatrix.Count; x++)
                if (symbol.ModuleMatrix[y][x])
                    Rect(220 + 3 * x, 65 + 3 * y, 3, 3, 0);
        }
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
            z.Write(pixels);
        return Document([
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 360 200] /Resources << /Font << /F1 4 0 R >> /XObject << /Scan 6 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(
                "q 360 0 0 200 0 0 cm /Scan Do Q"
                    + (ocr ? " BT /F1 17 Tf 3 Tr 18 166 Td (DEMO RX) Tj ET" : "")
            ),
            Stream(
                Encoding.Latin1.GetString(ms.ToArray()),
                "/Type /XObject /Subtype /Image /Width 360 /Height 200 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode"
            ),
        ]);
    }
}
