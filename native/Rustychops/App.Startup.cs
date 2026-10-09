using System.IO;
using System.Windows;

namespace Rustychops;

public partial class App
{
    private void StartNative(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rustychops", "error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{DateTime.Now:O}\n{args.Exception}\n");
            MessageBox.Show($"Rustychops encountered an error. Details were saved to {log}\n\n{args.Exception.Message}", "Rustychops");
            args.Handled = true;
        };
        MainWindow = new NativeWindow();
        MainWindow.Show();
    }
}
