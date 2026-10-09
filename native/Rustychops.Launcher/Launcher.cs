using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Rustychops Launcher")]
[assembly: AssemblyDescription("Launches the current local Rustychops Windows build")]
[assembly: AssemblyVersion("0.1.0.0")]

internal static class Launcher
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [STAThread]
    private static int Main()
    {
        string repository = AppDomain.CurrentDomain.BaseDirectory;
        string executable = Path.Combine(repository, "artifacts", "windows", "Rustychops.exe");
        try
        {
            if (!File.Exists(executable))
            {
                MessageBox.Show("The native Rustychops build was not found.\n\nFrom the repository folder, run:\n\nnpm run native:build\n\nKeep this launcher in the repository root, beside the artifacts folder.", "Rustychops Launcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
            foreach (Process process in Process.GetProcessesByName("Rustychops"))
            {
                using (process)
                {
                    try
                    {
                        if (string.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase) && process.MainWindowHandle != IntPtr.Zero)
                        {
                            ShowWindow(process.MainWindowHandle, 3); // Maximize within the Windows work area.
                            SetForegroundWindow(process.MainWindowHandle);
                            return 0;
                        }
                    }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (InvalidOperationException) { }
                }
            }
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = true
            });
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show("Could not launch Rustychops.\n\n" + error.Message, "Rustychops Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
