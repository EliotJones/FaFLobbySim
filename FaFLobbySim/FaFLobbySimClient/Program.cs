using System.Collections;
using System.Diagnostics;
using System.Drawing;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace FaFLobbySimClient;

internal delegate string GetFilePath(string fileName);

public static class Program
{
    public static void Main(string[] args)
    {
        var process = Process.GetProcesses().FirstOrDefault(x => x.MainWindowTitle == "Forged Alliance");
        if (process == null)
        {
            Console.WriteLine("FA is not running, exiting.");
            Console.ReadKey();
            return;
        }

        var screenid = Guid.NewGuid().ToString("D")[..8];
        GetFilePath pathGetter = fileName =>
        {
            var filenameonly = Path.GetFileNameWithoutExtension(fileName);

            var path = Path.Combine(@"D:\temp\", filenameonly + screenid + Path.GetExtension(fileName));

            return path;
        };

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
                    var screenshot = GetScreenshot(window, child, pathGetter);

                    var image = GetThresholdedImage(screenshot, pathGetter);

                    var sw = Stopwatch.StartNew();
                    // image = Image.Load<Rgb24>(@"C:\temp\tester.png");
                    WordDetection(image, pathGetter);
                    Console.WriteLine("Runtime was: " + sw.ElapsedMilliseconds + " milliseconds");
                }
            } while (child != null);
            var buttons = window.FindAllDescendants(x => x.ByControlType(ControlType.Button));
        }
        Console.WriteLine("Hello, World!");
    }

    private static CaptureImage GetScreenshot(Window window, AutomationElement pane, GetFilePath savePathGetter)
    {
        window.Focus();
        var ss = Capture.Element(pane);

        ss.ToFile(savePathGetter("original.png"));

        return ss;
    }

    private static Image<Rgb24> GetThresholdedImage(CaptureImage screenshot, GetFilePath savePathGetter)
    {
        using var memoryStream = new MemoryStream();
        // Assuming you have a System.Drawing.Bitmap object called 'bitmap'
        screenshot.Bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
        memoryStream.Seek(0, SeekOrigin.Begin);

        // Create an ImageSharp Image object from the MemoryStream
        Image<Rgba32> image = Image.Load<Rgba32>(memoryStream);

        image.Mutate(x => x.Grayscale().BinaryThreshold(0.4f));

        image.SaveAsPngAsync(savePathGetter("thresholded.png"));

        return image.CloneAs<Rgb24>();
    }

    private static Image WordDetection(Image<Rgb24> grayscaleImage, GetFilePath savePathGetter)
    {
        var wh = new WidthHeight(grayscaleImage.Width, grayscaleImage.Height);
        var image = FlattenThresholded(grayscaleImage, wh);
        var visited = new HashSet<int>();
        var inRegion = new HashSet<int>();
        var regions = new List<List<(int x, int y)>>();

        for (int row = 0; row < grayscaleImage.Height; row++)
        {
            for (int col = 0; col < grayscaleImage.Width; col++)
            {
                var flatIndex = (row * grayscaleImage.Width) + col;
                var p = image[flatIndex];

                if (!p)
                {
                    // Skip black pixels
                    continue;
                }

                if (visited.Contains(flatIndex))
                {
                    continue;
                }

                var size = 1;
                inRegion.Clear();
                GrowFromPixel(image, flatIndex, wh, visited, inRegion, ref size);

                var xy = inRegion.Select(i => FlatToXy(i, wh)).ToList();

                var minX = xy.MinBy(i => i.x);
                var maxX = xy.MaxBy(i => i.x);
                var minY = xy.MinBy(i => i.y);
                var maxY = xy.MaxBy(i => i.y);

                var height = maxY.y - minY.y;
                var width = maxX.x - minX.x;

                // Throw away some garbage.
                if (height > 30 || width > 120 || height <= 3 || width <= 2)
                {
                    continue;
                }

                regions.Add(xy);

                grayscaleImage[minX.x, minY.y] = new Rgb24(0, 0, 255);
                for (int i = minX.x + 1; i <= maxX.x; i++)
                {
                    grayscaleImage[i, minY.y] = new Rgb24(0, 20, 250);
                }

                for (int i = minY.y + 1; i <= maxY.y; i++)
                {
                    grayscaleImage[minX.x, i] = new Rgb24(10, 200, 0);
                }

                for (int i = minX.x; i < maxX.x; i++)
                {
                    grayscaleImage[i, maxY.y] = new Rgb24(200, 10, 0);
                }

                for (int i = minY.y; i < maxY.y; i++)
                {
                    grayscaleImage[maxX.x, i] = new Rgb24(100, 255, 0);
                }
            }
        }

        grayscaleImage.SaveAsPng(savePathGetter("whatever.png"));

        return grayscaleImage;
    }

    private static void GrowFromPixel(BitArray image, int index,
        WidthHeight widthHeight,
        HashSet<int> everVisited,
        HashSet<int> inRegionLocal,
        ref int size)
    {
        everVisited.Add(index);

        if (image[index])
        {
            inRegionLocal.Add(index);
        }

        if (size > 50000)
        {
            return;
        }

        foreach (var surrounding in GetLocalNeighborhood(index, widthHeight))
        {
            size += 1;
            if (everVisited.Contains(surrounding) || inRegionLocal.Contains(surrounding))
            {
                continue;
            }

            everVisited.Add(surrounding);

            if (!image[surrounding])
            {
                continue;
            }

            GrowFromPixel(image, surrounding, widthHeight, everVisited, inRegionLocal, ref size);
        }
    }

    private static IEnumerable<int> GetLocalNeighborhood(int index, WidthHeight widthHeight)
    {
        /*
         * Get a 9 by 9 square around the pixel of interest (O):
         *
         * FFFFFFFFF
         * FFFFFFFFF
         * FFFFFFFFF
         * FFFFFFFFF
         * FFFFOFFFF
         * FFFFFFFFF
         * FFFFFFFFF
         * FFFFFFFFF
         * FFFFFFFFF
         *
         * But actually because we scan left-right top-bottom we only want pixels following this pixel or to its top right:
         *
         * _____FFFF
         * _____FFFF
         * _____FFFF
         * _____FFFF
         * FFFFOFFFF
         * FFFFFFFFF
         * FFFFFFFFF
         * FFFFFFFFF
         * FFFFFFFFF
         */
        const int windowSize = 9;
        const int halfWay = 4;
        for (int x = 0; x < windowSize; x++)
        {
            for (int y = 0; y < windowSize; y++)
            {
                if (x == halfWay && y == halfWay)
                {
                    continue;
                }

                if (y < halfWay && x <= halfWay)
                {
                    continue;
                }

                if (TryMove(index, x - halfWay, y - halfWay, widthHeight, out var newIndex))
                {
                    yield return newIndex;
                }
            }
        }
    }

    private static int XyToFlat(int x, int y, WidthHeight widthHeight)
        => (y * widthHeight.Width) + x;

    private static (int x, int y) FlatToXy(int index, WidthHeight widthHeight)
    {
        var y = index / (widthHeight.Width);

        var x = index - (y * widthHeight.Width);

        return (x, y);
    }

    private static bool TryMove(int index, int dX, int dY, WidthHeight widthHeight, out int result)
    {
        result = 0;
        var (x, y) = FlatToXy(index, widthHeight);

        var newX = x + dX;

        if (newX < 0 || newX >= widthHeight.Width)
        {
            return false;
        }

        var newY = y + dY;

        if (newY < 0 || newY >= widthHeight.Height)
        {
            return false;
        }

        result = XyToFlat(newX, newY, widthHeight);

        return true;
    }

    private static BitArray FlattenThresholded(Image<Rgb24> image, WidthHeight wh)
    {
        var result = new BitArray(image.Height * image.Width);

        for (int row = 0; row < image.Height; row++)
        {
            for (int col = 0; col < image.Width; col++)
            {
                var flatIndex = XyToFlat(col, row, wh);
                if (image[col, row].R == 255)
                {
                    result[flatIndex] = true;
                }
            }
        }

        return result;
    }

    private record WidthHeight(int Width, int Height);
}