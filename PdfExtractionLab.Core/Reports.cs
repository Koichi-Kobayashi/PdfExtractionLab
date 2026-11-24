using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace PdfExtractionLab.Core;

public record BenchmarkReport(
    DateTimeOffset Started,
    Conditions Conditions,
    IReadOnlyList<Measurement> Measurements,
    IReadOnlyDictionary<string, string> Engines
);

public static class Reports
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Write(
        string extension,
        IReadOnlyList<Result> results,
        DateTimeOffset? compared,
        BenchmarkReport? benchmark,
        bool content
    )
    {
        string cpu = RuntimeInformation.ProcessArchitecture.ToString();
        if (OperatingSystem.IsWindows())
            cpu =
                Registry
                    .GetValue(
                        @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                        "ProcessorNameString",
                        cpu
                    )
                    ?.ToString()
                    ?.Trim()
                ?? cpu;
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        ulong? physical =
            OperatingSystem.IsWindows() && GlobalMemoryStatusEx(ref memory)
                ? memory.TotalPhysical
                : null;
        var model = new
        {
            Compared = compared,
            OS = RuntimeInformation.OSDescription,
            CPU = cpu,
            PhysicalMemoryBytes = physical,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            LogicalProcessors = Environment.ProcessorCount,
            Runtime = RuntimeInformation.FrameworkDescription,
            ComparisonConditions = "同一メモリバイト列・逐次・各1回・同一文書内でテキスト→座標→画像。合計は各段階の和。表示・保存は対象外。",
            Results = results.Select(r => new
            {
                r.Engine,
                r.Version,
                r.Capabilities,
                r.Times,
                Pages = r.Pages.Count,
                Utf16 = r.Pages.Sum(p => p.Text.Length),
                Scalars = r.Pages.Sum(p => p.Text.EnumerateRunes().Count()),
                Images = r.Pages.Sum(p => p.Images.Count),
                Words = r.Pages.Sum(p => p.Layout.Elements.Count(e => e.Kind == "単語")),
                Success = r.Error == null,
                Error = content ? r.Error : r.Error?.Split(':')[0],
                WarningCount = r.Warnings.Count,
                Warnings = content ? r.Warnings : null,
                Payload = content
                    ? r.Pages.Select(p => new
                    {
                        p.Number,
                        p.Text,
                        p.Layout,
                        Images = p.Images.Select(i => new
                        {
                            i.Index,
                            i.Width,
                            i.Height,
                            i.Pixels,
                            i.ColorSpace,
                            i.Bits,
                            i.Filter,
                            i.Method,
                            i.Format,
                            i.Raw,
                            i.X,
                            i.Y,
                        }),
                    })
                    : null,
            }),
            Benchmark = benchmark,
            ContainsContent = content,
        };
        var tree = JsonSerializer.SerializeToElement(model, Json);
        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            return tree.GetRawText();
        var lines = new List<string>();
        bool csv = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
        bool md = extension.Equals(".md", StringComparison.OrdinalIgnoreCase);
        if (csv)
            lines.Add("Field,Value");
        if (md)
            lines.Add("# PDF抽出比較\n\n| 項目 | 値 |\n|---|---|");
        Visit(tree, "");
        return string.Join(Environment.NewLine, lines);
        void Visit(JsonElement node, string path)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in node.EnumerateObject())
                    Visit(p.Value, path.Length == 0 ? p.Name : path + "." + p.Name);
                return;
            }
            if (node.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var child in node.EnumerateArray())
                    Visit(child, $"{path}[{i++}]");
                return;
            }
            string value = node.ValueKind == JsonValueKind.Null ? "" : node.ToString();
            lines.Add(
                csv ? $"{Quote(path)},{Quote(value)}"
                : md
                    ? $"| {path} | {System.Net.WebUtility.HtmlEncode(value).Replace("|", "&#124;").ReplaceLineEndings("<br>")} |"
                : $"{path} = {value}"
            );
        }
    }

    private static string Quote(string s)
    {
        if (s.TrimStart() is [var c, ..] && "=+-@".Contains(c))
            s = "'" + s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        internal uint Length,
            Load;
        internal ulong TotalPhysical,
            AvailablePhysical,
            TotalPageFile,
            AvailablePageFile,
            TotalVirtual,
            AvailableVirtual,
            AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
