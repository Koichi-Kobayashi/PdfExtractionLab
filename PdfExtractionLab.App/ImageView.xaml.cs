using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PdfExtractionLab.Core;

namespace PdfExtractionLab.App;

public partial class ImageView : UserControl
{
    public ImageView()
    {
        InitializeComponent();
    }

    private void SelectImage(object sender, SelectionChangedEventArgs e)
    {
        if (Preview == null)
            return;
        Preview.Source = null;
        Details.Text = "";
        if (Images.SelectedItem is not EmbeddedImage image)
            return;
        Details.Text =
            $"{image.Pixels:N0} pixels　{image.Bits} bit/成分　{image.ColorSpace}　{image.Filter}　左上 ({image.X:F1}, {image.Y:F1}) pt　原座標 {image.Raw}　Y軸下向き={image.RawTopDown}";
        try
        {
            using var stream = new MemoryStream(image.Preview);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = Math.Min(1300, image.Width);
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            Preview.Source = bitmap;
        }
        catch (Exception)
        {
            Details.Text += "　この形式のプレビューは未対応です。原本を保存できます。";
        }
    }

    private async void SaveOriginal(object s, RoutedEventArgs e) => await Save(false);

    private async void SavePreview(object s, RoutedEventArgs e) => await Save(true);

    private async Task Save(bool preview)
    {
        if (Images.SelectedItem is not EmbeddedImage im || (preview && Preview.Source == null))
            return;
        var bytes = preview ? im.Preview : im.Data;
        var ext = EmbeddedImage.Identify(bytes);
        var dlg = new SaveFileDialog
        {
            FileName = $"page-{im.Page}-image-{im.Index}.{ext}",
            Filter = $"画像データ|*.{ext}",
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                await SafeOutput.WriteAsync(
                    dlg.FileName,
                    bytes,
                    ((MainViewModel)DataContext).ResultPath
                );
            }
            catch (Exception ex)
            {
                Details.Text = ex.Message;
            }
        }
    }
}
