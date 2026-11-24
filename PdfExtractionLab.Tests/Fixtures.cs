using System.Text;

namespace PdfExtractionLab.Tests;

public static partial class Fixtures
{
    internal static byte[] English() => Single("BT /F1 14 Tf 40 200 Td (Synthetic sample) Tj ET");

    internal static byte[] Single(string commands, string pageOptions = "") =>
        Document([
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] {pageOptions} /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(commands),
        ]);

    internal static string Stream(string body, string options = "") =>
        $"<< /Length {Encoding.Latin1.GetByteCount(body)} {options} >>\nstream\n{body}\nendstream";

    internal static byte[] Document(IReadOnlyList<string> objects)
    {
        using var output = new MemoryStream();
        void Write(string s) => output.Write(Encoding.Latin1.GetBytes(s));
        Write("%PDF-1.7\n");
        var offsets = new List<long>();
        foreach (var obj in objects)
        {
            offsets.Add(output.Position);
            Write($"{offsets.Count} 0 obj\n{obj}\nendobj\n");
        }
        long start = output.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (long pos in offsets)
            Write($"{pos:D10} 00000 n \n");
        Write($"trailer\n<< /Root 1 0 R /Size {objects.Count + 1} >>\nstartxref\n{start}\n%%EOF\n");
        return output.ToArray();
    }
}
