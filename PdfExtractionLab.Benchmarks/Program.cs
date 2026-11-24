using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using PdfExtractionLab.Core;
using PdfExtractionLab.Engines.OnePdf;
using PdfExtractionLab.Engines.PdfPig;

if (Debugger.IsAttached)
    throw new InvalidOperationException("デバッガーを外して実行してください。");
#if DEBUG
throw new InvalidOperationException("Release構成で実行してください。");
#else
if (args.Length == 0 || !File.Exists(args[0]))
{
    Console.Error.WriteLine(
        "PDFパス [--quick | --cold] [--file] [--reopen] [BenchmarkDotNet options]"
    );
    return 2;
}
Environment.SetEnvironmentVariable("PDFLAB_INPUT", Path.GetFullPath(args[0]));
Environment.SetEnvironmentVariable("PDFLAB_SOURCE", args.Contains("--file") ? "File" : "Memory");
Environment.SetEnvironmentVariable("PDFLAB_LIFE", args.Contains("--reopen") ? "Reopen" : "Reuse");
bool cold = args.Contains("--cold");
var job = cold
    ? Job
        .Default.WithStrategy(RunStrategy.ColdStart)
        .WithLaunchCount(5)
        .WithWarmupCount(0)
        .WithIterationCount(1)
        .WithInvocationCount(1)
        .WithUnrollFactor(1)
    : Job
        .Default.WithLaunchCount(1)
        .WithWarmupCount(3)
        .WithIterationCount(args.Contains("--quick") ? 5 : 100);
var cfg = ManualConfig
    .Create(DefaultConfig.Instance)
    .AddJob(job)
    .AddColumn(
        StatisticColumn.Median,
        StatisticColumn.Min,
        StatisticColumn.Max,
        StatisticColumn.P95
    );
var cli = args.Skip(1)
    .Where(x => x is not ("--cold" or "--quick" or "--file" or "--reopen"))
    .ToArray();
if (!cli.Contains("--filter") && !cli.Contains("-f"))
    cli = [.. cli, "--filter", "*"];
var reports = BenchmarkSwitcher.FromTypes([typeof(PdfJobs)]).Run(cli, cfg).ToArray();
return
    reports.Length == 0
    || reports.Any(s =>
        s.HasCriticalValidationErrors || s.Reports.Count() == 0 || s.Reports.Any(r => !r.Success)
    )
    ? 1
    : 0;
#endif

[MemoryDiagnoser]
public class PdfJobs
{
    [Params("PdfPig", "OnePDF")]
    public string Library { get; set; } = "";
    private IPdfExtractionEngine engine = null!;
    private string path = "";
    private byte[] bytes = [];
    private InputSource source;
    private bool reuse;
    private IPdfSession? session;

    [GlobalSetup]
    public void Setup()
    {
        engine = Library == "PdfPig" ? new PigEngine() : new OneEngine();
        path = Environment.GetEnvironmentVariable("PDFLAB_INPUT")!;
        source = Enum.Parse<InputSource>(Environment.GetEnvironmentVariable("PDFLAB_SOURCE")!);
        reuse = Environment.GetEnvironmentVariable("PDFLAB_LIFE") == "Reuse";
        if (source == InputSource.Memory)
            bytes = File.ReadAllBytes(path);
    }

    [GlobalSetup(Targets = [nameof(Text), nameof(Layout), nameof(Images), nameof(Total)])]
    public void SetupExtraction()
    {
        Setup();
        if (reuse)
            session = OpenDocument();
    }

    [GlobalCleanup]
    public void Cleanup() => session?.Dispose();

    private IPdfSession OpenDocument() => BenchmarkWork.Open(engine, path, bytes, source);

    [Benchmark]
    public int Open()
    {
        using var d = OpenDocument();
        return d.PageCount;
    }

    [Benchmark]
    public int Text() => Run(Stage.Text);

    [Benchmark]
    public int Layout() => Run(Stage.Layout);

    [Benchmark]
    public int Images() => Run(Stage.Images);

    [Benchmark]
    public int Total() => Run(Stage.Total);

    private int Run(Stage stage)
    {
        if (session != null)
            return BenchmarkWork.Consume(session, stage, 1, 1, default);
        using var opened = OpenDocument();
        return BenchmarkWork.Consume(opened, stage, 1, 1, default);
    }
}
