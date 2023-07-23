using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Net.Http.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace FaFLobbySimClient;

public static class Program
{
    private static readonly Uri ServerUrl = new Uri("http://eliot-jones.com:6575/");

    private static int _testOccupancy = 1;
    private static int _testCounter = 0;

    public static async Task Main(string[] args)
    {
        var jobName = "paved-cabbage-21";

        var client = new HttpClient
        {
            BaseAddress = ServerUrl,
            Timeout = TimeSpan.FromSeconds(2)
        };

        var isTest = args.Any(x => string.Equals("test", x, StringComparison.OrdinalIgnoreCase));

        var occHandle = new CalculateLobbyOccupancyHandler();
        var wordDetector = new WordDetector(true);

        var focusWindow = new FocusWindowState
        {
            FailureCount = 0,
            FocusWindow = true
        };

        var monitor = isTest ? 
            new LobbyMonitor(
                (out CaptureImage? screenshot) =>
                {
                    screenshot = new CaptureImage(new Bitmap(20, 20), Rectangle.Empty, new CaptureSettings());
                    return true;
                },
                (image, getter) => new List<WordRegion>
                {
                    new WordRegion()
                },
                (regions, height) =>
                {
                    _testCounter++;

                    if (_testCounter > 10)
                    {
                        _testCounter = 0;
                        if (_testOccupancy > 3)
                        {
                            _testOccupancy -= 1;
                        }
                        else
                        {
                            _testOccupancy += 1;
                        }
                    }

                    return new Occupancy(10, _testOccupancy);
                },
                async (identifier, occupancy) => await WriteOccupancy(occupancy, identifier, client),
                (output, warning) => {}
                )
            : new LobbyMonitor(
            (out CaptureImage? screenshot) => TryScreenshot(focusWindow, out screenshot),
            wordDetector.Detect,
            occHandle.Calculate,
            async (identifier, occupancy) => await WriteOccupancy(occupancy, identifier, client),
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

        await monitor.Monitor(jobName);

        Console.WriteLine("Run completed");
    }

    private static async Task WriteOccupancy(Occupancy occupancy, string identifier, HttpClient client)
    {
        try
        {
            // Garbage data.
            if (occupancy.Total == 0 || occupancy.Occupied > occupancy.Total)
            {
                return;
            }

            await client.PostAsJsonAsync("upload", new
            {
                identifier = identifier,
                occupied = occupancy.Occupied,
                total = occupancy.Total,
                clientId = "me"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed writing occupancy to server: ");
            Console.WriteLine(ex);
        }
    }

    private static bool TryScreenshot(FocusWindowState focusWindowState, [NotNullWhen(true)] out CaptureImage? screenshot)
    {
        screenshot = default;
        try
        {
            var process = Process.GetProcesses().FirstOrDefault(x => x.MainWindowTitle == "Forged Alliance");
            if (process == null)
            {
                focusWindowState.FocusWindow = true;
                focusWindowState.FailureCount = 0;
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
                    if (focusWindowState.FocusWindow)
                    {
                        focusWindowState.FocusWindow = false;
                        window.Focus();
                    }

                    screenshot = GetScreenshot(child);
                    return true;
                }
            } while (child != null);
        }
        catch (Exception ex)
        {
            focusWindowState.FailureCount++;

            if (focusWindowState.FailureCount > 10)
            {
                focusWindowState.FocusWindow = true;
                focusWindowState.FailureCount = 0;
            }

            Console.WriteLine(ex);
        }

        return false;
    }

    private static CaptureImage GetScreenshot(AutomationElement pane)
    {
        var imageCapture = Capture.Element(pane);

        return imageCapture;
    }

    private class FocusWindowState
    {
        public bool FocusWindow { get; set; }

        public int FailureCount { get; set; }
    }
}

internal delegate void WriteOutput(string output, bool isWarning);

internal delegate bool TryScreenshotProcess([NotNullWhen(true)] out CaptureImage? screenshot);

internal delegate Task WriteOccupancyForJob(string identifier, Occupancy occupancy);