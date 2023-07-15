using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FaFLobbySimClient;

internal delegate IReadOnlyList<WordRegion> DetectImageWords(Image<Rgb24> image, GetFilePath filePathGetter);

internal class WordDetector
{
    private readonly bool _saveFilesLocally;

    public WordDetector(bool saveFilesLocally)
    {
        _saveFilesLocally = saveFilesLocally;
    }

    public IReadOnlyList<WordRegion> Detect(Image<Rgb24> input, GetFilePath filePathGetter)
    {
        var grayscaleImage = GetThresholdedImage(input, filePathGetter);

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

                if (TryConvertRegion(inRegion, wh, grayscaleImage, out var region))
                {
                    regions.Add(region);
                }
            }
        }

        Console.WriteLine("Words detected in: " + sw.ElapsedMilliseconds + " milliseconds");

        if (_saveFilesLocally)
        {
            grayscaleImage.SaveAsPng(filePathGetter("words-detected.png"));
        }

        return regions;
    }

    private Image<Rgb24> GetThresholdedImage(Image<Rgb24> image, GetFilePath savePathGetter)
    { 
        image.Mutate(x => x.BinaryThreshold(0.39f));

        if (_saveFilesLocally)
        {
            image.SaveAsPng(savePathGetter("thresholded.png"));
        }

        return image.CloneAs<Rgb24>();
    }

    private static bool TryConvertRegion(
        HashSet<int> pixels,
        WidthHeight wh,
        Image<Rgb24> image,
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
        if (height > 30 || width > 250 || height <= 3 || width <= 2)
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