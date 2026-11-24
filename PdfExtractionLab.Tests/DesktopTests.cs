using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PdfExtractionLab.App;
using PdfExtractionLab.Core;
using Xunit;

namespace PdfExtractionLab.Tests;

public class DesktopTests
{
    [Fact]
    public void ExtractsOriginalJpegAndInvisibleJapanese()
    {
        var (pdf, jpeg) = Fixtures.JapaneseScan();
        foreach (var engine in ExtractionTests.Engines)
        {
            var result = Extraction.Run(engine, pdf, default);
            Assert.Null(result.Error);
            var page = Assert.Single(result.Pages);
            Assert.Contains("架空処方箋", page.Text);
            Assert.Contains(
                page.Layout.Elements,
                e => e.Text == "架" && e.Visibility!.Contains("Tr=3")
            );
            Assert.Equal(jpeg, Assert.Single(page.Images).Data);
        }
    }

    [Fact]
    public async Task DesktopComparesAndCancelsWhileDispatcherRuns()
    {
        string dir = Directory.CreateTempSubdirectory("PdfLabDesktop-").FullName;
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App.App();
                app.InitializeComponent();
                app.StartupUri = new Uri(
                    "/PdfExtractionLab.App;component/MainWindow.xaml",
                    UriKind.Relative
                );
                app.Startup += (_, _) =>
                    app.Dispatcher.BeginInvoke(
                        DispatcherPriority.ApplicationIdle,
                        async () =>
                        {
                            var window = (MainWindow)app.MainWindow;
                            try
                            {
                                var vm = (MainViewModel)window.DataContext;
                                var path = Path.Combine(dir, "many.pdf");
                                File.WriteAllBytes(path, Fixtures.Large());
                                vm.Path = path;
                                var running = vm.CompareCommand.ExecuteAsync(null);
                                bool dispatchDuringWork = false;
                                await window.Dispatcher.InvokeAsync(
                                    () => dispatchDuringWork = vm.Busy,
                                    DispatcherPriority.Input
                                );
                                await running;
                                Assert.True(dispatchDuringWork);
                                Assert.Equal(300, vm.Left?.Pages.Count);
                                Assert.Equal(300, vm.Right?.Pages.Count);
                                Assert.Equal("Demo", ((TextBox)window.FindName("LeftBody")).Text);
                                vm.Path = Path.Combine(dir, "large-scan.pdf");
                                await Task.Run(() => Fixtures.WriteLargeScan(vm.Path));
                                int pulses = 0;
                                var timer = new DispatcherTimer
                                {
                                    Interval = TimeSpan.FromMilliseconds(16),
                                };
                                timer.Tick += (_, _) =>
                                {
                                    if (
                                        vm.Busy
                                        && vm.Status.StartsWith("PdfPig", StringComparison.Ordinal)
                                    )
                                        pulses++;
                                };
                                timer.Start();
                                await vm.CompareCommand.ExecuteAsync(null);
                                timer.Stop();
                                Assert.True(
                                    pulses > 0,
                                    "解析中のDispatcherタイマーが発火しませんでした。"
                                );
                                Assert.Equal(32, vm.Left?.Pages.Count);
                                Assert.Equal(32, vm.Right?.Pages.Count);
                                void CancelDuringPage(
                                    object? s,
                                    System.ComponentModel.PropertyChangedEventArgs args
                                )
                                {
                                    if (
                                        args.PropertyName == nameof(vm.Status)
                                        && vm.Status.StartsWith("PdfPig", StringComparison.Ordinal)
                                    )
                                        vm.CancelCommand.Execute(null);
                                }
                                vm.PropertyChanged += CancelDuringPage;
                                await vm.CompareCommand.ExecuteAsync(null);
                                vm.PropertyChanged -= CancelDuringPage;
                                Assert.Contains("キャンセル", vm.Status);
                                vm.Path = Path.Combine(dir, "short.pdf");
                                File.WriteAllBytes(vm.Path, Fixtures.English());
                                vm.Repeats = "2";
                                await vm.BenchmarkCommand.ExecuteAsync(null);
                                Assert.Equal(10, vm.Rows.Count);
                                Assert.All(vm.Rows, r => Assert.Equal(2, r.Success));
                                completion.TrySetResult();
                            }
                            catch (Exception e)
                            {
                                completion.TrySetException(e);
                            }
                            finally
                            {
                                window.Close();
                            }
                        }
                    );
                app.DispatcherUnhandledException += (_, e) =>
                {
                    completion.TrySetException(e.Exception);
                    e.Handled = true;
                    app.Shutdown();
                };
                app.Run();
            }
            catch (Exception e)
            {
                completion.TrySetException(e);
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(90));
        }
        finally
        {
            thread.Join(TimeSpan.FromSeconds(5));
            foreach (string file in Directory.EnumerateFiles(dir))
                File.Delete(file);
            Directory.Delete(dir);
        }
    }
}
