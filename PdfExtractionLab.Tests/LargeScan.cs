using System.Text;

namespace PdfExtractionLab.Tests;

public static partial class Fixtures
{
    // Write separate 1 MiB scan streams directly so fixture construction doesn't dominate peak memory.
    internal static void WriteLargeScan(string path, int count = 32)
    {
        using var file = File.Create(path);
        void Write(string s) => file.Write(Encoding.ASCII.GetBytes(s));
        var offsets = new List<long>();
        void Begin()
        {
            offsets.Add(file.Position);
            Write($"{offsets.Count} 0 obj\n");
        }
        void End() => Write("\nendobj\n");
        Write("%PDF-1.7\n");
        Begin();
        Write("<< /Type /Catalog /Pages 2 0 R >>");
        End();
        Begin();
        Write(
            $"<< /Type /Pages /Count {count} /Kids ["
                + string.Join(" ", Enumerable.Range(0, count).Select(i => $"{4 + i * 2} 0 R"))
                + "] >>"
        );
        End();
        Begin();
        Write(Stream("q 512 0 0 512 0 0 cm /Scan Do Q"));
        End();
        var scan = new byte[1024 * 1024];
        for (int i = 0; i < scan.Length; i++)
            scan[i] = (byte)(200 + (i / 1024 + i % 1024) % 56);
        for (int i = 0; i < count; i++)
        {
            Begin();
            Write(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 512 512] /Resources << /XObject << /Scan {5 + i * 2} 0 R >> >> /Contents 3 0 R >>"
            );
            End();
            scan[0] = (byte)i;
            Begin();
            Write(
                $"<< /Type /XObject /Subtype /Image /Width 1024 /Height 1024 /ColorSpace /DeviceGray /BitsPerComponent 8 /Length {scan.Length} >>\nstream\n"
            );
            file.Write(scan);
            Write("\nendstream");
            End();
        }
        long start = file.Position;
        Write($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (long pos in offsets)
            Write($"{pos:D10} 00000 n \n");
        Write($"trailer\n<< /Root 1 0 R /Size {offsets.Count + 1} >>\nstartxref\n{start}\n%%EOF\n");
    }
}
