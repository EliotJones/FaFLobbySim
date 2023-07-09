using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

                    var image = GetThresholdedImage(screenshot, PathGetter);

                    var regions = WordDetection(image, PathGetter);

                    var sw = Stopwatch.StartNew();

                    var occupancy = occHandle.Calculate(regions, new WidthHeight(image.Width, image.Height));

                    Console.WriteLine("Runtime of occupancy calculation was: " + sw.ElapsedMilliseconds + " milliseconds");
                }
            } while (child != null);
        }
        Console.WriteLine("Hello, World!");
    }

    private static CaptureImage GetScreenshot(Window window, AutomationElement pane, GetFilePath savePathGetter)
    {
        window.Focus();
        var imageCapture = Capture.Element(pane);

        imageCapture.ToFile(savePathGetter("original.png"));

        return imageCapture;
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

    private static IReadOnlyList<WordRegion> WordDetection(Image<Rgb24> grayscaleImage, GetFilePath savePathGetter)
    {
        var sw = Stopwatch.StartNew();

        var wh = new WidthHeight(grayscaleImage.Width, grayscaleImage.Height);
        var image = FlattenThresholded(grayscaleImage, wh);
        var visited = new HashSet<int>();
        var inRegion = new HashSet<int>();
        var regions = new List<WordRegion>();

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

                if (TryConvertRegion(inRegion, wh, grayscaleImage, savePathGetter, out var region))
                {
                    regions.Add(region);
                }
            }
        }

        Console.WriteLine("Words detected in: " + sw.ElapsedMilliseconds + " milliseconds");

        grayscaleImage.SaveAsPng(savePathGetter("words-detected.png"));

        return regions;
    }

    private static bool TryConvertRegion(
        HashSet<int> pixels, 
        WidthHeight wh,
        Image<Rgb24> image,
        GetFilePath filePathGetter,
       [NotNullWhen(true)] out WordRegion? region)
    {
        region = null;
        if (pixels.Count < 5)
        {
            return false;
        }

        var xy = pixels.Select(i =>
        {
            var (x, y) = FlatToXy(i, wh);

            return new FafPoint((short)x, (short)y);
        }).ToArray();

        var minX = xy.MinBy(i => i.X);
        var maxX = xy.MaxBy(i => i.X);
        var minY = xy.MinBy(i => i.Y);
        var maxY = xy.MaxBy(i => i.Y);

        var rectangle = new FafRectangle(
            new FafPoint(minX.X, minY.Y),
            new FafPoint(maxX.X, maxY.Y));

        var height = rectangle.Height;
        var width = rectangle.Width;

        // Throw away some garbage/noise.
        if (height > 30 || width > 120 || height <= 3 || width <= 2)
        {
            return false;
        }

        image[minX.X, minY.Y] = new Rgb24(0, 0, 255);
        for (int i = minX.X + 1; i <= maxX.X; i++)
        {
            image[i, minY.Y] = new Rgb24(0, 20, 250);
        }

        for (int i = minY.Y + 1; i <= maxY.Y; i++)
        {
            image[minX.X, i] = new Rgb24(10, 200, 0);
        }

        for (int i = minX.X; i < maxX.X; i++)
        {
            image[i, maxY.Y] = new Rgb24(200, 10, 0);
        }

        for (int i = minY.Y; i < maxY.Y; i++)
        {
            image[maxX.X, i] = new Rgb24(100, 255, 0);
        }

        region = new WordRegion
        {
            Bounds = rectangle,
            Points = xy
        };

        return true;
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

}