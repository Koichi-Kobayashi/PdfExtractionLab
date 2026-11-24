using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace PdfExtractionLab.App;

public partial class MainWindow : Window
{
    internal readonly MainViewModel vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = vm;
        InitializePanels();
    }

    partial void InitializePanels();

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!vm.Busy && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths)
            vm.Path = paths[0];
        e.Handled = true;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void DescribeCoordinates(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (
            e.Column is DataGridTextColumn column
            && column.Binding is System.Windows.Data.Binding binding
        )
            binding.TargetNullValue = "未取得";
    }

    private bool scrolling;

    private void SyncScroll(object sender, ScrollChangedEventArgs e)
    {
        if (
            scrolling
            || LeftBody == null
            || RightBody == null
            || (e.VerticalChange == 0 && e.HorizontalChange == 0)
        )
            return;
        scrolling = true;
        try
        {
            var other = sender == LeftBody ? RightBody : LeftBody;
            other.ScrollToVerticalOffset(e.VerticalOffset);
            other.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
        finally
        {
            scrolling = false;
        }
    }

    private void ThemeClick(object sender, RoutedEventArgs e) =>
        ApplicationThemeManager.Apply(
            ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark
                ? ApplicationTheme.Light
                : ApplicationTheme.Dark
        );
}
