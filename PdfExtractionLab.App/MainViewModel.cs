using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PdfExtractionLab.Core;
using PdfExtractionLab.Engines.OnePdf;
using PdfExtractionLab.Engines.PdfPig;

namespace PdfExtractionLab.App;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private TextOrder order;
    public TextOrder[] Orders => Enum.GetValues<TextOrder>();
    public IPdfExtractionEngine[] Engines => [new PigEngine(Order), new OneEngine(Order)];

    [ObservableProperty]
    private bool wholeDocument;
    public string DisplayLeft => WholeDocument ? Left?.FullText ?? "" : LeftText;
    public string DisplayRight => WholeDocument ? Right?.FullText ?? "" : RightText;

    partial void OnWholeDocumentChanged(bool value) => Refresh();

    public string Versions =>
        string.Join("     /     ", Engines.Select(e => $"{e.Name}  {e.Version}"));

    [ObservableProperty]
    private string path = "";

    [ObservableProperty]
    private string status = "PDFを選択、またはここにドロップしてください。";

    [ObservableProperty]
    private bool busy;

    [ObservableProperty]
    private int page;

    [ObservableProperty]
    private int[] pageNumbers = [];

    [ObservableProperty]
    private double fontSize = 16;

    [ObservableProperty]
    private Result? left;

    [ObservableProperty]
    private Result? right;
    public string LeftText => Current(Left)?.Text ?? "";
    public string RightText => Current(Right)?.Text ?? "";

    public PageResult? Current(Result? r) => r?.Pages.FirstOrDefault(p => p.Number == Page);

    partial void OnPageChanged(int value) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(LeftText));
        OnPropertyChanged(nameof(RightText));
        OnPropertyChanged(nameof(DisplayLeft));
        OnPropertyChanged(nameof(DisplayRight));
        RefreshDetails();
    }

    partial void RefreshDetails();

    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFileDialog { Filter = "PDF|*.pdf" };
        if (dialog.ShowDialog() == true)
            Path = dialog.FileName;
    }

    private CancellationTokenSource? cancellation;

    [RelayCommand]
    private void Cancel() => cancellation?.Cancel();

    private async Task Execute(Func<CancellationToken, Task> action)
    {
        if (Busy)
            return;
        Busy = true;
        using var source = new CancellationTokenSource();
        cancellation = source;
        try
        {
            await action(source.Token);
        }
        catch (OperationCanceledException)
        {
            Status = "キャンセルしました。";
        }
        catch (Exception ex)
        {
            Status = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            cancellation = null;
            Busy = false;
        }
    }

    [RelayCommand]
    private Task CompareAsync() =>
        Execute(async token =>
        {
            Status = "読み込み中…";
            var sourcePath = System.IO.Path.GetFullPath(Path);
            var engines = Engines;
            var bytes = await File.ReadAllBytesAsync(sourcePath, token);
            var progress = new Progress<string>(s => Status = s);
            var results = await Task.Run(
                () =>
                    engines
                        .Select(e =>
                            Extraction.Run(
                                e,
                                bytes,
                                token,
                                (p, n) =>
                                {
                                    if (p == 1 || p % 10 == 0 || p == n)
                                        ((IProgress<string>)progress).Report(
                                            $"{e.Name}   {p} / {n}"
                                        );
                                }
                            )
                        )
                        .ToArray(),
                token
            );
            token.ThrowIfCancellationRequested();
            ResultPath = sourcePath;
            ComparedHash = await Task.Run(
                () => System.Security.Cryptography.SHA256.HashData(bytes),
                token
            );
            Left = results[0];
            Right = results[1];
            PageNumbers = Enumerable.Range(1, results.Max(r => r.Pages.Count)).ToArray();
            Page = PageNumbers.FirstOrDefault();
            Refresh();
            Status = results.Any(r => r.Error != null)
                ? "エラーの詳細を確認してください。"
                : "比較が完了しました。";
            Compared();
        });

    partial void Compared();
}
