using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using SixLabors.ImageSharp.PixelFormats;
using Image = SixLabors.ImageSharp.Image;

namespace FaFLobbySimClient;

public static class Program
{
    public static void Main(string[] args)
    {
        var occHandle = new CalculateLobbyOccupancyHandler();

        var process = Process.GetProcesses().FirstOrDefault(x => x.MainWindowTitle == "Forged Alliance");
        if (process == null)
        {
            Console.WriteLine("FA is not running, exiting.");
            Console.ReadKey();
            return;
        }

        var screenid = Guid.NewGuid().ToString("D")[..8];

        string PathGetter(string fileName)
        {
            var filenameonly = Path.GetFileNameWithoutExtension(fileName);

            var path = Path.Combine(@"D:\temp\", filenameonly + screenid + Path.GetExtension(fileName));

            return path;
        }

        var wordDetector = new WordDetector(true);

        var app = Application.Attach(process);
        using (var auto = new UIA3Automation())
        {
            var window = app.GetMainWindow(auto);
            var walker = auto.TreeWalkerFactory.GetRawViewWalker();
            AutomationElement? child = window;
            do
            {
                child = walker.GetFirstChild(child);

                if (child?.ControlType == ControlType.Pane)
                {
                    var screenshot = GetScreenshot(window, child, PathGetter);

                    using var memoryStream = new MemoryStream();

                    screenshot.Bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    // Create an ImageSharp Image object from the MemoryStream
                    var image = Image.Load<Rgb24>(memoryStream);

                    var regions = wordDetector.Detect(image, PathGetter);

                    var sw = Stopwatch.StartNew();

                    var occupancy = occHandle.Calculate(regions, new WidthHeight(image.Width, image.Height));

                    Console.WriteLine("Runtime of occupancy calculation was: " + sw.ElapsedMilliseconds + " milliseconds");
                }
            } while (child != null);
        }

        Console.WriteLine("Run completed");
    }

    private static CaptureImage GetScreenshot(Window window, AutomationElement pane, GetFilePath savePathGetter)
    {
        window.Focus();
        var imageCapture = Capture.Element(pane);

        imageCapture.ToFile(savePathGetter("original.png"));

        return imageCapture;
    }
}