using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace FaFLobbySimClient;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var occHandle = new CalculateLobbyOccupancyHandler();
        var wordDetector = new WordDetector(true);

        var monitor = new LobbyMonitor(
            TryScreenshot,
            wordDetector.Detect,
            occHandle.Calculate,
            (output, warning) =>
            {
                var current = Console.ForegroundColor;
                if (warning)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }

                Console.WriteLine(output);

                Console.ForegroundColor = current;
            });

        await monitor.Monitor();

        Console.WriteLine("Run completed");
    }

    private static bool TryScreenshot(out CaptureImage screenshot)
    {
        screenshot = default;
        try
        {
            var process = Process.GetProcesses().FirstOrDefault(x => x.MainWindowTitle == "Forged Alliance");
            if (process == null)
            {
                return false;
            }

            using var app = Application.Attach(process);
            using var automation = new UIA3Automation();
            var window = app.GetMainWindow(automation);
            var walker = automation.TreeWalkerFactory.GetRawViewWalker();
            AutomationElement? child = window;
            do
            {
                child = walker.GetFirstChild(child);

                if (child?.ControlType == ControlType.Pane)
                {
                    screenshot = GetScreenshot(window, child);
                    return true;
                }
            } while (child != null);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        return false;
    }

    private static CaptureImage GetScreenshot(Window window, AutomationElement pane)
    {
        window.Focus();
        var imageCapture = Capture.Element(pane);

        return imageCapture;
    }
}

internal delegate void WriteOutput(string output, bool isWarning);

internal delegate bool TryScreenshotProcess(out CaptureImage screenshot);