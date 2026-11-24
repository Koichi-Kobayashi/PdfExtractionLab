using System.Windows.Controls;

namespace PdfExtractionLab.App;

public partial class BenchmarkView : UserControl
{
    public BenchmarkView()
    {
        InitializeComponent();
    }
}

public partial class MainWindow
{
    partial void AddBenchmarkPanel() => BenchPanel.Content = new BenchmarkView();
}
