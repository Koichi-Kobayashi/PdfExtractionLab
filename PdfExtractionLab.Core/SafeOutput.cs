namespace PdfExtractionLab.Core;

public static class SafeOutput
{
    public static async Task WriteAsync(
        string destination,
        byte[] data,
        params string?[] protectedInputs
    )
    {
        if (
            protectedInputs.Any(p =>
                p != null
                && string.Equals(
                    Path.GetFullPath(p),
                    Path.GetFullPath(destination),
                    StringComparison.OrdinalIgnoreCase
                )
            ) || Path.GetExtension(destination).Equals(".pdf", StringComparison.OrdinalIgnoreCase)
        )
            throw new IOException("PDFへの上書きはできません。");
        string temp = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(destination))!,
            Path.GetRandomFileName()
        );
        try
        {
            await File.WriteAllBytesAsync(temp, data);
            File.Move(temp, destination, true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
