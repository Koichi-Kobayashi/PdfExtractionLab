using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PdfExtractionLab.Core;

namespace PdfExtractionLab.App;

public partial class MainViewModel
{
    public ComparisonMode[] Modes => Enum.GetValues<ComparisonMode>();

    [ObservableProperty]
    private ComparisonMode mode;
    public string[] Pairs { get; } = ["PdfPig → OnePDF", "正解 → PdfPig", "正解 → OnePDF"];

    [ObservableProperty]
    private int pair;

    [ObservableProperty]
    private string? expected;

    [ObservableProperty]
    private Difference[] differences = [];

    [ObservableProperty]
    private string diffStatus = "";

    [ObservableProperty]
    private int side;

    [ObservableProperty]
    private string kind = "文字";
    public string[] Kinds { get; } = ["文字", "単語", "行", "ブロック"];
    public string[] Sides { get; } = ["PdfPig", "OnePDF"];
    public PageResult? Selected => Current(Side == 0 ? Left : Right);
    public Element[] Elements =>
        Selected?.Layout.Elements.Where(e => e.Kind == Kind).ToArray() ?? [];
    public string Geometry =>
        Selected?.Layout is { } l
            ? $"{l.Width:F1} × {l.Height:F1} pt　回転 {l.Rotation}°　Media {l.Media}　Crop {l.Crop}"
            : "";
    private int revision;

    partial void OnModeChanged(ComparisonMode value) => UpdateDiff();

    partial void OnPairChanged(int value) => UpdateDiff();

    partial void OnExpectedChanged(string? value) => UpdateDiff();

    partial void OnSideChanged(int value) => Refresh();

    partial void OnKindChanged(string value) => Refresh();

    partial void RefreshDetails()
    {
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(Elements));
        OnPropertyChanged(nameof(Geometry));
        UpdateDiff();
    }

    [RelayCommand]
    private async Task LoadExpectedAsync()
    {
        var dlg = new OpenFileDialog { Filter = "正解テキスト UTF-8|*.txt" };
        if (dlg.ShowDialog() != true)
            return;
        try
        {
            Expected = await File.ReadAllTextAsync(
                dlg.FileName,
                new System.Text.UTF8Encoding(false, true)
            );
            Pair = 1;
        }
        catch (Exception e)
        {
            Status = e.Message;
        }
    }

    private async void UpdateDiff()
    {
        int version = ++revision;
        Differences = [];
        if (
            Page < 1
            || (Pair != 2 && Current(Left) == null)
            || (Pair != 1 && Current(Right) == null)
        )
        {
            DiffStatus = "比較するページがありません。";
            return;
        }
        if (Pair != 0 && Expected == null)
        {
            DiffStatus = "正解テキストを選択してください。";
            return;
        }
        string? a = Pair == 0 ? LeftText : Expected!.Split('\f').ElementAtOrDefault(Page - 1);
        if (a == null)
        {
            DiffStatus = "正解テキストにこのページがありません。";
            return;
        }
        string b = Pair == 1 ? LeftText : RightText;
        var selected = Mode;
        DiffStatus = "計算中…";
        try
        {
            var changes = await Task.Run(() => TextComparison.Compare(a, b, selected));
            if (version != revision)
                return;
            Differences = changes;
            DiffStatus = $"{selected}　差分 {changes.Length} 件　位置は0始まりのUnicodeスカラー";
        }
        catch (Exception ex)
        {
            if (version == revision)
                DiffStatus = ex.Message;
        }
    }
}
