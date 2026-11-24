namespace PdfExtractionLab.App;

public partial class MainWindow
{
    partial void InitializePanels()
    {
        ImagePanel.Content = new ImageView();
        AddBenchmarkPanel();
    }

    partial void AddBenchmarkPanel();
}
