using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PdfExtractionLab.Core;

namespace PdfExtractionLab.App;

public partial class MainViewModel
{
    [ObservableProperty]
    private string repeats = "100";

    [ObservableProperty]
    private bool warmup = true;

    [ObservableProperty]
    private InputSource source;

    [ObservableProperty]
    private Lifetime life;

    [ObservableProperty]
    private int workers = 1;

    [ObservableProperty]
    private bool includeContent;

    [ObservableProperty]
    private string log = "";

    [ObservableProperty]
    private string benchSummary = "";

    [ObservableProperty]
    private IReadOnlyList<Measurement> rows = [];
    public InputSource[] Sources => Enum.GetValues<InputSource>();
    public Lifetime[] Lifetimes => Enum.GetValues<Lifetime>();
    public int[] WorkerOptions { get; } = [1, 2, 4];
    internal string? ResultPath { get; private set; }
    internal byte[] ComparedHash { get; private set; } = [];
    private string? benchmarkPath;
    private BenchmarkReport? measured;
    private DateTimeOffset? compared;

    partial void Compared()
    {
        compared = DateTimeOffset.Now;
        measured = null;
        Rows = [];
        BenchSummary = "";
        Log = string.Join(
            "\n\n",
            new[] { Left!, Right! }.Select(r =>
                $"{r.Engine} {r.Version}\n{r.Capabilities}\n{r.Error ?? "成功"}\n"
                + string.Join("\n", r.Warnings)
            )
        );
        OnPropertyChanged(nameof(Relative));
    }

    public string Relative =>
        Left is { Error: null } l && Right is { Error: null } r && r.Times.Open > 0
            ? $"開く時間比 PdfPig / OnePDF = {l.Times.Open / r.Times.Open:F2}　（各1回）"
            : "";

    [RelayCommand]
    private Task BenchmarkAsync() =>
        Execute(async token =>
        {
            if (!int.TryParse(Repeats, out int count) || count < 1)
                throw new ArgumentException("繰り返し回数には正の整数を指定してください。");
            var options = new Conditions(Source, Life, count, Workers, Warmup);
            var path = System.IO.Path.GetFullPath(Path);
            var engines = Engines;
            // A shared read handle keeps the input stable throughout both file-mode runs.
            using var guard = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var data =
                options.Source == InputSource.Memory
                    ? await File.ReadAllBytesAsync(path, token)
                    : [];
            byte[] hash = await System.Security.Cryptography.SHA256.HashDataAsync(guard, token);
            var started = DateTimeOffset.Now;
            IProgress<string> progress = new Progress<string>(s => Status = s);
            Status = "測定中…";
            var all = await Task.Run(
                () =>
                    engines
                        .SelectMany(e =>
                            BenchmarkWork.Measure(
                                e,
                                path,
                                data,
                                options,
                                token,
                                (stage, i) =>
                                {
                                    if (i == 1 || i % Math.Max(1, count / 10) == 0 || i == count)
                                        progress.Report($"{e.Name}　{stage}　{i}/{count}");
                                }
                            )
                        )
                        .ToArray(),
                token
            );
            // Keep a benchmark independent from a previous comparison of a different input.
            if (!hash.SequenceEqual(ComparedHash))
            {
                Left = null;
                Right = null;
                PageNumbers = [];
                Page = 0;
                compared = null;
                ResultPath = null;
                Refresh();
                OnPropertyChanged(nameof(Relative));
            }
            benchmarkPath = path;
            Rows = all;
            measured = new(
                started,
                options,
                all,
                engines.ToDictionary(e => e.Name, e => e.Version + "　" + e.Capabilities)
            );
            var opens = all.Where(m => m.Stage == Stage.Open).ToArray();
            BenchSummary =
                $"{options.Source} / {options.Lifetime} / {count}回 / {Workers}ワーカー　"
                + (
                    opens.All(o => o.MeanMs > 0)
                        ? $"Open 平均時間比 PdfPig / OnePDF = {opens[0].MeanMs / opens[1].MeanMs:F2}"
                        : "Open 比率なし"
                );
            string messages = string.Join(
                "\n",
                all.Where(m => m.Failure != 0)
                    .Select(m => $"{m.Engine} {m.Stage}: {m.Error} ({m.Failure})")
            );
            Log = (Left == null ? "" : Log + "\n\n") + BenchSummary + "\n" + messages;
            Status = "測定完了　" + BenchSummary;
        });

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Busy || (Left == null && measured == null))
        {
            Status = "先に比較または測定を実行してください。";
            return;
        }
        var dlg = new SaveFileDialog
        {
            Filter = "JSON|*.json|CSV|*.csv|Markdown|*.md|テキスト|*.txt",
            FileName = "comparison",
        };
        if (dlg.ShowDialog() != true)
            return;
        try
        {
            var results = new[] { Left, Right }.OfType<Result>().ToArray();
            var report = measured;
            var date = compared;
            bool payload = IncludeContent;
            var output = await Task.Run(() =>
                Reports.Write(
                    System.IO.Path.GetExtension(dlg.FileName),
                    results,
                    date,
                    report,
                    payload
                )
            );
            if (
                !string.IsNullOrWhiteSpace(Path)
                && string.Equals(
                    System.IO.Path.GetFullPath(dlg.FileName),
                    System.IO.Path.GetFullPath(Path),
                    StringComparison.OrdinalIgnoreCase
                )
            )
                throw new IOException("入力ファイルと異なる出力先を指定してください。");
            await SafeOutput.WriteAsync(
                dlg.FileName,
                Encoding.UTF8.GetBytes(output),
                ResultPath,
                benchmarkPath
            );
            Status = "結果を保存しました。";
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }
}
